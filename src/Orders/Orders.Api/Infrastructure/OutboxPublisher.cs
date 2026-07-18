using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Orders.Api.Infrastructure;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqSettings settings,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    // Static ActivitySource — must be static so OpenTelemetry can subscribe to it
    // Creating it inline (new ActivitySource(...)) means OTel never sees the spans
    private static readonly ActivitySource ActivitySource =
        new("Orders.Api.OutboxPublisher");

    private static readonly TextMapPropagator Propagator =
        Propagators.DefaultTextMapPropagator;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task PublishPendingMessages(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        logger.LogInformation("Publishing {Count} outbox messages", pending.Count);

        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port     = settings.Port,
            UserName = settings.Username,
            Password = settings.Password
        };

        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel    = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange:          settings.ExchangeName,
            type:              ExchangeType.Topic,
            durable:           true,
            cancellationToken: ct);

        foreach (var message in pending)
        {
            try
            {
                // ── Trace context propagation ──────────────────────────
                // Use the static ActivitySource — OTel is subscribed to this
                using var activity = ActivitySource.StartActivity(
                    "outbox.publish",
                    ActivityKind.Producer);

                activity?.SetTag("messaging.system",      "rabbitmq");
                activity?.SetTag("messaging.destination", settings.ExchangeName);
                activity?.SetTag("messaging.message_id",  message.Id.ToString());
                activity?.SetTag("messaging.operation",   "publish");
                activity?.SetTag("events.event_type",     message.EventType);

                var body       = Encoding.UTF8.GetBytes(message.Payload);
                var routingKey = message.EventType
                    .ToLower()
                    .Replace("event", "")
                    .Trim('.');

                // Inject W3C trace context into AMQP message headers
                var headers = new Dictionary<string, object?>();
                Propagator.Inject(
                    new PropagationContext(
                        activity?.Context ?? Activity.Current?.Context ?? default,
                        Baggage.Current),
                    headers,
                    (carrier, key, value) => carrier[key] = value);

                var props = new BasicProperties
                {
                    MessageId    = message.Id.ToString(),
                    ContentType  = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers      = headers
                };

                await channel.BasicPublishAsync(
                    exchange:          settings.ExchangeName,
                    routingKey:        routingKey,
                    mandatory:         false,
                    basicProperties:   props,
                    body:              body,
                    cancellationToken: ct);

                message.ProcessedAt = DateTime.UtcNow;

                logger.LogInformation(
                    "Published {EventType} with MessageId {MessageId} TraceId {TraceId}",
                    message.EventType,
                    message.Id,
                    activity?.TraceId.ToString() ?? "none");
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                logger.LogError(ex,
                    "Failed to publish message {MessageId}", message.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}