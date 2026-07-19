using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Features.Orders;

public static class GetOrderSummary
{
    public record Response(
        int     OrderId,
        int     CustomerId,
        string  Status,
        decimal Total,
        int     ItemCount,
        string  ItemsSummary,
        DateTime OrderCreatedAt,
        DateTime ProjectedAt);

    public static async Task<IResult> Handle(int id, OrdersDbContext db)
    {
        var summary = await db.OrderSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == id);

        if (summary is null)
            return Results.NotFound(
                $"No summary found for Order {id}. " +
                $"It may not have been projected yet — try again in a few seconds.");

        return Results.Ok(new Response(
            summary.OrderId,
            summary.CustomerId,
            summary.Status,
            summary.Total,
            summary.ItemCount,
            summary.ItemsSummary,
            summary.OrderCreatedAt,
            summary.ProjectedAt));
    }
}