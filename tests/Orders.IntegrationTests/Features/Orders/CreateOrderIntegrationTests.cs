using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;
using Orders.Api.Features.Orders;
using Orders.IntegrationTests.Fixtures;

namespace Orders.IntegrationTests.Features.Orders;

public class CreateOrderIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CreateOrderIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Customer> CreateTestCustomerAsync()
    {
        var customer = new Customer
        {
            FullName = "Test Customer",
            Email    = $"test-{Guid.NewGuid()}@example.com"
        };
        _fixture.DbContext.Customers.Add(customer);
        await _fixture.DbContext.SaveChangesAsync();
        return customer;
    }

    private async Task<Product> CreateTestProductAsync(string sku, decimal price)
    {
        var product = new Product
        {
            Name          = $"Product {sku}",
            SKU           = sku,
            Price         = price,
            StockQuantity = 100
        };
        _fixture.DbContext.Products.Add(product);
        await _fixture.DbContext.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_WritesOrderAndOutboxInSameTransaction()
    {
        // Arrange
        var customer = await CreateTestCustomerAsync();
        var product  = await CreateTestProductAsync($"SKU-{Guid.NewGuid()}", 99.99m);

        var request = new CreateOrder.Request(
            CustomerId: customer.Id,
            Items: [new CreateOrder.OrderItemRequest(product.Id, 2)]
        );

        // Act
        var result = await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert — order was created
        var orders = await _fixture.DbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customer.Id)
            .ToListAsync();

        Assert.Single(orders);
        Assert.Single(orders[0].Items);
        Assert.Equal(2, orders[0].Items.First().Quantity);
        Assert.Equal(99.99m, orders[0].Items.First().UnitPrice);

        // Assert — outbox message was written in SAME transaction
        var outboxMessages = await _fixture.DbContext.OutboxMessages
            .Where(m => m.EventType == "OrderCreatedEvent")
            .ToListAsync();

        Assert.NotEmpty(outboxMessages);
        Assert.Contains(outboxMessages, m => m.ProcessedAt == null);
    }

    [Fact]
    public async Task CreateOrder_InvalidCustomer_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateOrder.Request(
            CustomerId: 99999,
            Items: [new CreateOrder.OrderItemRequest(1, 1)]
        );

        // Act
        var result = await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound<string>>(result);
    }

    [Fact]
    public async Task CreateOrder_InvalidProduct_ReturnsBadRequest()
    {
        // Arrange
        var customer = await CreateTestCustomerAsync();
        var request  = new CreateOrder.Request(
            CustomerId: customer.Id,
            Items: [new CreateOrder.OrderItemRequest(99999, 1)]
        );

        // Act
        var result = await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreateOrder_ZeroQuantity_ReturnsBadRequest()
    {
        // Arrange
        var customer = await CreateTestCustomerAsync();
        var product  = await CreateTestProductAsync($"SKU-{Guid.NewGuid()}", 50m);
        var request  = new CreateOrder.Request(
            CustomerId: customer.Id,
            Items: [new CreateOrder.OrderItemRequest(product.Id, 0)]
        );

        // Act
        var result = await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreateOrder_DuplicateProducts_ReturnsBadRequest()
    {
        // Arrange
        var customer = await CreateTestCustomerAsync();
        var product  = await CreateTestProductAsync($"SKU-{Guid.NewGuid()}", 50m);
        var request  = new CreateOrder.Request(
            CustomerId: customer.Id,
            Items:
            [
                new CreateOrder.OrderItemRequest(product.Id, 1),
                new CreateOrder.OrderItemRequest(product.Id, 2) // duplicate
            ]
        );

        // Act
        var result = await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreateOrder_MultipleItems_CalculatesTotalCorrectly()
    {
        // Arrange
        var customer  = await CreateTestCustomerAsync();
        var product1  = await CreateTestProductAsync($"SKU-{Guid.NewGuid()}", 100m);
        var product2  = await CreateTestProductAsync($"SKU-{Guid.NewGuid()}", 50m);

        var request = new CreateOrder.Request(
            CustomerId: customer.Id,
            Items:
            [
                new CreateOrder.OrderItemRequest(product1.Id, 2), // 200
                new CreateOrder.OrderItemRequest(product2.Id, 3)  // 150
            ]
        );

        // Act
        await CreateOrder.Handle(request, _fixture.DbContext);

        // Assert total in outbox payload
        var outbox = await _fixture.DbContext.OutboxMessages
            .OrderByDescending(m => m.CreatedAt)
            .FirstAsync();

        Assert.Contains("350", outbox.Payload); // 200 + 150 = 350
    }
}