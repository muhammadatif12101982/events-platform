using Microsoft.EntityFrameworkCore;
using Orders.Api;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventsdb_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();

    public OrdersDbContext DbContext { get; private set; } = null!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        DbContext = new OrdersDbContext(options);
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _container.DisposeAsync();
    }
}