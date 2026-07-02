using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;

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

        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = orderItems
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var total = orderItems.Sum(i => i.UnitPrice * i.Quantity);
        return Results.Created($"/orders/{order.Id}",
            new Response(order.Id, order.Status.ToString(), total, order.CreatedAt));
    }
}