using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace Orders.Api.Infrastructure;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqSettings settings,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
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

            // Poll every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task PublishPendingMessages(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        // Find unprocessed messages
        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        logger.LogInformation("Publishing {Count} outbox messages", pending.Count);

        // Connect to RabbitMQ
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port     = settings.Port,
            UserName = settings.Username,
            Password = settings.Password
        };

        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Declare exchange (idempotent — safe to call every time)
        await channel.ExchangeDeclareAsync(
            exchange: settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: ct);

        foreach (var message in pending)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);
                var routingKey = message.EventType.ToLower()
                    .Replace("event", "")
                    .Trim('.');

                var props = new BasicProperties
                {
                    MessageId    = message.Id.ToString(),
                    ContentType  = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers      = new Dictionary<string, object?>
                    {
                        ["EventType"] = message.EventType
                    }
                };

                await channel.BasicPublishAsync(
                    exchange: settings.ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: ct);

                // Mark as processed
                message.ProcessedAt = DateTime.UtcNow;

                logger.LogInformation(
                    "Published {EventType} with MessageId {MessageId}",
                    message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                logger.LogError(ex, "Failed to publish message {MessageId}", message.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}