namespace Orders.Api.Domain.Events;

public record OrderCreatedEvent(
    int OrderId,
    int CustomerId,
    decimal Total,
    DateTime CreatedAt,
    List<OrderCreatedEvent.OrderItem> Items)
{
    public record OrderItem(int ProductId, string ProductName, int Quantity, decimal UnitPrice);
}