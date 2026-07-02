using Orders.Api.Domain;
namespace Orders.UnitTests.Domain;

public class OrderCalculationsTests
{
    // ---------------------------------------------------------------
    // CalculateTotal tests
    // ---------------------------------------------------------------

    [Fact]
    public void CalculateTotal_SingleItem_ReturnsCorrectTotal()
    {
        // Arrange
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(ProductId: 1, Quantity: 2, UnitPrice: 10.00m)
        };

        // Act
        var total = OrderCalculations.CalculateTotal(items);

        // Assert
        Assert.Equal(20.00m, total);
    }

    [Fact]
    public void CalculateTotal_MultipleItems_ReturnsSumOfAllLines()
    {
        // Arrange
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 2, 10.00m),   // 20.00
            new OrderCalculations.OrderLineItem(2, 1, 15.50m),   // 15.50
            new OrderCalculations.OrderLineItem(3, 3, 5.00m)     // 15.00
        };

        // Act
        var total = OrderCalculations.CalculateTotal(items);

        // Assert
        Assert.Equal(50.50m, total);
    }

    [Fact]
    public void CalculateTotal_EmptyList_ReturnsZero()
    {
        // Arrange
        var items = Enumerable.Empty<OrderCalculations.OrderLineItem>();

        // Act
        var total = OrderCalculations.CalculateTotal(items);

        // Assert
        Assert.Equal(0m, total);
    }

    [Fact]
    public void CalculateTotal_DecimalPrices_CalculatesCorrectly()
    {
        // Arrange
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 3, 9.99m)   // 29.97
        };

        // Act
        var total = OrderCalculations.CalculateTotal(items);

        // Assert
        Assert.Equal(29.97m, total);
    }

    // ---------------------------------------------------------------
    // HasValidQuantities tests
    // ---------------------------------------------------------------

    [Fact]
    public void HasValidQuantities_AllPositiveQuantities_ReturnsTrue()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 1, 10m),
            new OrderCalculations.OrderLineItem(2, 5, 20m)
        };

        Assert.True(OrderCalculations.HasValidQuantities(items));
    }

    [Fact]
    public void HasValidQuantities_ZeroQuantity_ReturnsFalse()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 0, 10m)
        };

        Assert.False(OrderCalculations.HasValidQuantities(items));
    }

    [Fact]
    public void HasValidQuantities_NegativeQuantity_ReturnsFalse()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, -1, 10m)
        };

        Assert.False(OrderCalculations.HasValidQuantities(items));
    }

    // ---------------------------------------------------------------
    // HasDuplicateProducts tests
    // ---------------------------------------------------------------

    [Fact]
    public void HasDuplicateProducts_UniqueProductIds_ReturnsFalse()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 1, 10m),
            new OrderCalculations.OrderLineItem(2, 1, 20m)
        };

        Assert.False(OrderCalculations.HasDuplicateProducts(items));
    }

    [Fact]
    public void HasDuplicateProducts_DuplicateProductId_ReturnsTrue()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 1, 10m),
            new OrderCalculations.OrderLineItem(1, 2, 10m)   // same product twice
        };

        Assert.True(OrderCalculations.HasDuplicateProducts(items));
    }

    [Fact]
    public void HasDuplicateProducts_SingleItem_ReturnsFalse()
    {
        var items = new[]
        {
            new OrderCalculations.OrderLineItem(1, 1, 10m)
        };

        Assert.False(OrderCalculations.HasDuplicateProducts(items));
    }
}