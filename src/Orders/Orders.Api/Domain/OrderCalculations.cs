namespace Orders.Api.Domain;

public static class OrderCalculations
{
    public record OrderLineItem(int ProductId, int Quantity, decimal UnitPrice);

    public static decimal CalculateTotal(IEnumerable<OrderLineItem> items)
    {
        return items.Sum(i => i.Quantity * i.UnitPrice);
    }

    public static bool HasValidQuantities(IEnumerable<OrderLineItem> items)
    {
        return items.All(i => i.Quantity > 0);
    }

    public static bool HasDuplicateProducts(IEnumerable<OrderLineItem> items)
    {
        var ids = items.Select(i => i.ProductId).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}