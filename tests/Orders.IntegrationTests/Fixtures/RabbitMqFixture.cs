using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Orders.IntegrationTests.Fixtures;

public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3.13-management-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    public string Host     { get; private set; } = null!;
    public int    Port     { get; private set; }
    public string Username => "testuser";
    public string Password => "testpass";
    public IConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Host = _container.Hostname;
        Port = _container.GetMappedPublicPort(5672);

        var factory = new ConnectionFactory
        {
            HostName = Host,
            Port     = Port,
            UserName = Username,
            Password = Password
        };

        for (var i = 0; i < 5; i++)
        {
            try
            {
                Connection = await factory.CreateConnectionAsync();
                break;
            }
            catch { await Task.Delay(TimeSpan.FromSeconds(2)); }
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection != null)
            await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}