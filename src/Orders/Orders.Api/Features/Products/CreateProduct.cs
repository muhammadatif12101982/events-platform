using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;

namespace Orders.Api.Features.Products;

public static class CreateProduct
{
    public record Request(string Name, string SKU, decimal Price, int StockQuantity);
    public record Response(int Id, string Name, string SKU, decimal Price, int StockQuantity);

    public static async Task<IResult> Handle(Request request, OrdersDbContext db)
    {
        var exists = await db.Products.AnyAsync(p => p.SKU == request.SKU);
        if (exists)
            return Results.Conflict($"Product with SKU '{request.SKU}' already exists.");

        var product = new Product
        {
            Name = request.Name,
            SKU = request.SKU,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        var response = new Response(product.Id, product.Name, product.SKU, product.Price, product.StockQuantity);
        return Results.Created($"/products/{product.Id}", response);
    }
}