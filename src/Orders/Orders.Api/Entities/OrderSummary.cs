namespace Orders.Api.Entities;

public class OrderSummary
{
    public int    Id             { get; set; }
    public int    OrderId        { get; set; }
    public int    CustomerId     { get; set; }
    public string Status         { get; set; } = "Pending";
    public decimal Total         { get; set; }
    public int    ItemCount      { get; set; }
    public string ItemsSummary   { get; set; } = string.Empty;
    public DateTime OrderCreatedAt  { get; set; }
    public DateTime ProjectedAt     { get; set; }

    // Index for fast lookup by OrderId
    // Separate from the write-side Orders table
}