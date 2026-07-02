using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Features.Products;

public static class ListProducts
{
    public record Response(int Id, string Name, string SKU, decimal Price, int StockQuantity);

    public static async Task<IResult> Handle(OrdersDbContext db)
    {
        var products = await db.Products
            .AsNoTracking()
            .Select(p => new Response(p.Id, p.Name, p.SKU, p.Price, p.StockQuantity))
            .ToListAsync();

        return Results.Ok(products);
    }
}