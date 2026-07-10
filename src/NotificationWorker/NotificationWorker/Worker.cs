using System.Text;
using System.Text.Json;
using NotificationWorker.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationWorker;

public class Worker(
    RabbitMqSettings settings,
    ILogger<Worker> logger) : BackgroundService
{
    // Track processed event IDs for idempotency
    // In production this would be a DB table or Redis set
    private readonly HashSet<string> _processedEventIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification Worker starting");

        // Retry connection loop — RabbitMQ may not be ready immediately
        IConnection? connection = null;
        while (connection == null && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    UserName = settings.Username,
                    Password = settings.Password
                };
                connection = await factory.CreateConnectionAsync(stoppingToken);
                logger.LogInformation("Connected to RabbitMQ");
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

            // Declare exchange — must match what Orders API publishes to
            await channel.ExchangeDeclareAsync(
                exchange: settings.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                cancellationToken: stoppingToken);

            // Declare queue
            await channel.QueueDeclareAsync(
                queue: settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // Bind queue to exchange with routing key
            await channel.QueueBindAsync(
                queue: settings.QueueName,
                exchange: settings.ExchangeName,
                routingKey: settings.RoutingKey,
                cancellationToken: stoppingToken);

            // Only fetch one message at a time — process before getting next
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: stoppingToken);

            logger.LogInformation(
                "Listening on queue '{Queue}' bound to exchange '{Exchange}' with key '{Key}'",
                settings.QueueName, settings.ExchangeName, settings.RoutingKey);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

                try
                {
                    // Idempotency check — skip if already processed
                    if (_processedEventIds.Contains(messageId))
                    {
                        logger.LogWarning(
                            "Duplicate message {MessageId} — skipping", messageId);
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (orderEvent == null)
                    {
                        logger.LogError("Failed to deserialize message {MessageId}", messageId);
                        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                        return;
                    }

                    // Process the notification
                    await ProcessNotificationAsync(orderEvent, messageId);

                    // Mark as processed (idempotency)
                    _processedEventIds.Add(messageId);

                    // Acknowledge — remove from queue
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error processing message {MessageId}", messageId);

                    // Nack without requeue — goes to dead letter queue if configured
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                }
            };

            await channel.BasicConsumeAsync(
                queue: settings.QueueName,
                autoAck: false,   // Manual ack — we control when message is removed
                consumer: consumer,
                cancellationToken: stoppingToken);

            // Keep alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private Task ProcessNotificationAsync(OrderCreatedEvent orderEvent, string messageId)
    {
        // In production: send email, SMS, push notification
        // For now: structured log simulating notification sent
        logger.LogInformation(
            "📧 NOTIFICATION SENT — Order #{OrderId} for Customer #{CustomerId} " +
            "| Total: {Total:C} | Items: {ItemCount} | MessageId: {MessageId}",
            orderEvent.OrderId,
            orderEvent.CustomerId,
            orderEvent.Total,
            orderEvent.Items.Count,
            messageId);

        foreach (var item in orderEvent.Items)
        {
            logger.LogInformation(
                "   → {ProductName} x{Quantity} @ {UnitPrice:C}",
                item.ProductName, item.Quantity, item.UnitPrice);
        }

        return Task.CompletedTask;
    }
}