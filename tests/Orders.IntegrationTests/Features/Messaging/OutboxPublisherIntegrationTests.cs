using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Api;
using Orders.Api.Entities;
using Orders.Api.Infrastructure;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Orders.IntegrationTests.Features.Messaging;

public class OutboxPublisherIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventsdb_messaging_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3.13-management-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    private OrdersDbContext _db = null!;
    private IConnection _connection = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new OrdersDbContext(options);
        await _db.Database.MigrateAsync();

        var factory = new ConnectionFactory
        {
            HostName = _rabbitmq.Hostname,
            Port     = _rabbitmq.GetMappedPublicPort(5672),
            UserName = "testuser",
            Password = "testpass"
        };

        for (var i = 0; i < 5; i++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync();
                break;
            }
            catch { await Task.Delay(TimeSpan.FromSeconds(2)); }
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
    }

    [Fact]
    public async Task OutboxPublisher_ProcessesPendingMessage_PublishesToRabbitMQ()
    {
        // Arrange — insert a pending outbox message
        var outboxMessage = new OutboxMessage
        {
            EventType = "OrderCreatedEvent",
            Payload   = """{"orderId":1,"customerId":1,"total":99.99,"items":[]}"""
        };
        _db.OutboxMessages.Add(outboxMessage);
        await _db.SaveChangesAsync();

        // Set up RabbitMQ consumer BEFORE starting publisher
        var channel = await _connection.CreateChannelAsync();
        var tcs     = new TaskCompletionSource<string>();

        await channel.ExchangeDeclareAsync(
            exchange: "events-platform",
            type: ExchangeType.Topic,
            durable: true);

        await channel.QueueDeclareAsync(
            queue: "test-queue",
            durable: false,
            exclusive: true,
            autoDelete: true);

        await channel.QueueBindAsync(
            queue: "test-queue",
            exchange: "events-platform",
            routingKey: "ordercreated");

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            tcs.TrySetResult(body);
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(
            queue: "test-queue",
            autoAck: true,
            consumer: consumer);

        // Act — set up publisher pointing at test containers
        var connectionString = _postgres.GetConnectionString();
        var rabbitHost       = _rabbitmq.Hostname;
        var rabbitPort       = _rabbitmq.GetMappedPublicPort(5672);

        var services = new ServiceCollection();
        services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(connectionString));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory    = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger        = loggerFactory.CreateLogger<OutboxPublisher>();

        // Use test container credentials and mapped port
        var settings = new RabbitMqSettings
        {
            Host     = rabbitHost,
            Port     = rabbitPort,
            Username = "testuser",
            Password = "testpass"
        };

        var publisher = new OutboxPublisher(scopeFactory, settings, logger);
        var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await publisher.StartAsync(cts.Token);

        // Assert — message received within 15 seconds
        var receivedMessage = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains("orderId", receivedMessage, StringComparison.OrdinalIgnoreCase);

        // Assert — outbox marked as processed
        await _db.Entry(outboxMessage).ReloadAsync();
        Assert.NotNull(outboxMessage.ProcessedAt);

        await publisher.StopAsync(CancellationToken.None);
    }
}