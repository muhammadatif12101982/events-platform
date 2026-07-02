namespace Orders.Api.Entities;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string SKU { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}