using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Orders.Api.Domain.Events;
using Orders.Api.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics.Metrics;

namespace Orders.Api.Infrastructure;

public class OrderProjection(
    IServiceScopeFactory scopeFactory,
    RabbitMqSettings settings,
    ILogger<OrderProjection> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource =
        new("Orders.Api.OrderProjection");

    private static readonly TextMapPropagator Propagator =
        Propagators.DefaultTextMapPropagator;

    // Custom metric — projection lag in milliseconds
    private static readonly Meter Meter =
        new("Orders.Api.OrderProjection");

    private static readonly Histogram<double> ProjectionLag =
        Meter.CreateHistogram<double>(
            name:        "order_projection_lag_ms",
            unit:        "ms",
            description: "Time between order creation and read model projection");

    // Idempotency — track processed message IDs
    private readonly HashSet<string> _processedIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Order projection starting");

        IConnection? connection = null;
        while (connection == null && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    Port     = settings.Port,
                    UserName = settings.Username,
                    Password = settings.Password
                };
                connection = await factory.CreateConnectionAsync(stoppingToken);
                logger.LogInformation("Order projection connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ not ready, retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (connection == null) return;

        using (connection)
        {
            var channel = await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

            // Same exchange as the publisher
            await channel.ExchangeDeclareAsync(
                exchange:          settings.ExchangeName,
                type:              ExchangeType.Topic,
                durable:           true,
                cancellationToken: stoppingToken);

            // DIFFERENT queue from notification-worker
            // Each consumer gets its own independent copy of every event
            const string queueName = "order-projections";

            await channel.QueueDeclareAsync(
                queue:             queueName,
                durable:           true,
                exclusive:         false,
                autoDelete:        false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue:             queueName,
                exchange:          settings.ExchangeName,
                routingKey:        "ordercreated",
                cancellationToken: stoppingToken);

            await channel.BasicQosAsync(
                prefetchSize:  0,
                prefetchCount: 1,
                global:        false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var messageId = ea.BasicProperties.MessageId
                    ?? Guid.NewGuid().ToString();

                try
                {
                    if (_processedIds.Contains(messageId))
                    {
                        logger.LogWarning(
                            "Duplicate projection message {MessageId} — skipping",
                            messageId);
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    // Extract trace context from headers
                    var headers = ea.BasicProperties.Headers
                        ?? new Dictionary<string, object?>();

                    var parentContext = Propagator.Extract(
                        default,
                        headers,
                        (carrier, key) =>
                        {
                            if (carrier.TryGetValue(key, out var value))
                            {
                                if (value is byte[] bytes)
                                    return [Encoding.UTF8.GetString(bytes)];
                                if (value is string str)
                                    return [str];
                            }
                            return [];
                        });

                    Baggage.Current = parentContext.Baggage;

                    using var activity = ActivitySource.StartActivity(
                        "order.project",
                        ActivityKind.Consumer,
                        parentContext.ActivityContext);

                    activity?.SetTag("messaging.system",     "rabbitmq");
                    activity?.SetTag("messaging.message_id", messageId);
                    activity?.SetTag("projection.type",      "OrderSummary");

                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(
                        body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (orderEvent == null)
                    {
                        logger.LogError(
                            "Failed to deserialize projection message {MessageId}",
                            messageId);
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                        return;
                    }

                    await ProjectOrderAsync(orderEvent, activity);

                    _processedIds.Add(messageId);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);

                    logger.LogInformation(
                        "Projected OrderSummary for Order #{OrderId}",
                        orderEvent.OrderId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error projecting message {MessageId}", messageId);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
            };

            await channel.BasicConsumeAsync(
                queue:   queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private async Task ProjectOrderAsync(
        OrderCreatedEvent orderEvent,
        Activity? activity)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<OrdersDbContext>();

        // Idempotent upsert — safe to run multiple times
        var existing = await db.OrderSummaries
            .FirstOrDefaultAsync(s => s.OrderId == orderEvent.OrderId);

        var itemsSummary = string.Join(", ",
            orderEvent.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));

        var projectedAt = DateTime.UtcNow;

        if (existing == null)
        {
            db.OrderSummaries.Add(new OrderSummary
            {
                OrderId       = orderEvent.OrderId,
                CustomerId    = orderEvent.CustomerId,
                Status        = "Pending",
                Total         = orderEvent.Total,
                ItemCount     = orderEvent.Items.Count,
                ItemsSummary  = itemsSummary,
                OrderCreatedAt = orderEvent.CreatedAt,
                ProjectedAt   = projectedAt
            });
        }
        else
        {
            // Idempotent update — same data, just refresh
            existing.Status       = "Pending";
            existing.Total        = orderEvent.Total;
            existing.ItemCount    = orderEvent.Items.Count;
            existing.ItemsSummary = itemsSummary;
            existing.ProjectedAt  = projectedAt;
        }

        await db.SaveChangesAsync();

        // Record projection lag as a metric tag on the span
        var lag = projectedAt - orderEvent.CreatedAt;
        
        // Record as a Prometheus metric
        ProjectionLag.Record(lag.TotalMilliseconds);

        activity?.SetTag("projection.lag_ms", lag.TotalMilliseconds);

        logger.LogInformation(
            "OrderSummary projected for Order #{OrderId} — lag: {LagMs}ms",
            orderEvent.OrderId,
            lag.TotalMilliseconds);
    }
}