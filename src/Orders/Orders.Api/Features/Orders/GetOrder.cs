using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Features.Orders;

public static class GetOrder
{
    public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice);
    public record Response(int Id, int CustomerId, string Status, decimal Total, DateTime CreatedAt, List<OrderItemResponse> Items);

    public static async Task<IResult> Handle(int id, OrdersDbContext db)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return Results.NotFound($"Order {id} not found.");

        var items = order.Items.Select(i =>
            new OrderItemResponse(i.ProductId, i.Product.Name, i.Quantity, i.UnitPrice))
            .ToList();

        var total = items.Sum(i => i.UnitPrice * i.Quantity);

        return Results.Ok(new Response(order.Id, order.CustomerId,
            order.Status.ToString(), total, order.CreatedAt, items));
    }
}