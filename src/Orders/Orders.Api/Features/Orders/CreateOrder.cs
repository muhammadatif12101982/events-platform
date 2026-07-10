using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;
using Orders.Api.Domain;
using System.Text.Json;
using Orders.Api.Domain.Events;

namespace Orders.Api.Features.Orders;

public static class CreateOrder
{
    public record OrderItemRequest(int ProductId, int Quantity);
    public record Request(int CustomerId, List<OrderItemRequest> Items);
    public record Response(int Id, string Status, decimal Total, DateTime CreatedAt);

    public static async Task<IResult> Handle(Request request, OrdersDbContext db)
    {
        // Validate customer exists
        var customerExists = await db.Customers.AnyAsync(c => c.Id == request.CustomerId);
        if (!customerExists)
            return Results.NotFound($"Customer {request.CustomerId} not found.");

        var validationItems = request.Items.Select(i =>
            new OrderCalculations.OrderLineItem(i.ProductId, i.Quantity, 0m)).ToList();

        if (!OrderCalculations.HasValidQuantities(validationItems))
            return Results.BadRequest("All item quantities must be greater than zero.");

        if (OrderCalculations.HasDuplicateProducts(validationItems))
            return Results.BadRequest("Duplicate products in order. Combine quantities instead.");

        // Validate all products exist and have stock
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        if (products.Count != productIds.Count)
            return Results.BadRequest("One or more products not found.");

        // Build order items
        var orderItems = request.Items.Select(item =>
        {
            var product = products.First(p => p.Id == item.ProductId);
            return new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            };
        }).ToList();

        // Calculate total BEFORE using it in the outbox event
        var lineItems = orderItems
            .Select(i => new OrderCalculations.OrderLineItem(i.ProductId, i.Quantity, i.UnitPrice));
        var total = OrderCalculations.CalculateTotal(lineItems);

        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = orderItems
        };
        db.Orders.Add(order);

        // Write outbox message in the SAME transaction as the order
        // This guarantees either both succeed or both fail — no dual-write problem
        var domainEvent = new OrderCreatedEvent(
            OrderId: 0, // will be set after SaveChanges
            CustomerId: order.CustomerId,
            Total: total,
            CreatedAt: order.CreatedAt,
            Items: orderItems.Select(i => new OrderCreatedEvent.OrderItem(
                i.ProductId,
                products.First(p => p.Id == i.ProductId).Name,
                i.Quantity,
                i.UnitPrice)).ToList()
        );

        var outboxMessage = new OutboxMessage
        {
            EventType = nameof(OrderCreatedEvent),
            Payload = JsonSerializer.Serialize(domainEvent)
        };
        db.OutboxMessages.Add(outboxMessage);

        await db.SaveChangesAsync();

        // Update the event with the real OrderId now that we have it
        outboxMessage.Payload = JsonSerializer.Serialize(domainEvent with { OrderId = order.Id });
        await db.SaveChangesAsync();

        // Replace this:
        //var total = orderItems.Sum(i => i.UnitPrice * i.Quantity);

        return Results.Created($"/orders/{order.Id}",
            new Response(order.Id, order.Status.ToString(), total, order.CreatedAt));
    }
}