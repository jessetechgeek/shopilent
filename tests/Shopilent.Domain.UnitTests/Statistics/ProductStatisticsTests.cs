using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Statistics;

namespace Shopilent.Domain.Tests.Statistics;

public class ProductStatisticsTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateProductStatistics()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productName = "Test Product";
        var viewCount = 100;
        var orderCount = 10;
        var quantitySold = 15;
        var revenueResult = Money.FromDollars(750);
        revenueResult.IsSuccess.Should().BeTrue();
        var revenue = revenueResult.Value;

        // Act
        var productStatistics = ProductStatistics.Create(
            productId,
            productName,
            viewCount,
            orderCount,
            quantitySold,
            revenue);

        // Assert
        productStatistics.ProductId.Should().Be(productId);
        productStatistics.ProductName.Should().Be(productName);
        productStatistics.ViewCount.Should().Be(viewCount);
        productStatistics.OrderCount.Should().Be(orderCount);
        productStatistics.QuantitySold.Should().Be(quantitySold);
        productStatistics.Revenue.Should().Be(revenue);
        productStatistics.LastUpdated.Should().BeOnOrBefore(DateTime.UtcNow);
        productStatistics.LastUpdated.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void IncrementViews_ShouldIncrementViewCount()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productName = "Test Product";
        var initialViewCount = 100;
        var orderCount = 10;
        var quantitySold = 15;
        var revenueResult = Money.FromDollars(750);
        revenueResult.IsSuccess.Should().BeTrue();
        var revenue = revenueResult.Value;

        var productStatistics = ProductStatistics.Create(
            productId,
            productName,
            initialViewCount,
            orderCount,
            quantitySold,
            revenue);

        // Act
        var updatedStatistics = productStatistics.IncrementViews();

        // Assert
        updatedStatistics.ViewCount.Should().Be(initialViewCount + 1);
        updatedStatistics.ProductId.Should().Be(productId);
        updatedStatistics.ProductName.Should().Be(productName);
        updatedStatistics.OrderCount.Should().Be(orderCount);
        updatedStatistics.QuantitySold.Should().Be(quantitySold);
        updatedStatistics.Revenue.Should().Be(revenue);
    }

    [Fact]
    public void AddSale_ShouldIncrementOrderCountAndQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productName = "Test Product";
        var viewCount = 100;
        var initialOrderCount = 10;
        var initialQuantitySold = 15;
        var initialRevenueResult = Money.FromDollars(750);
        initialRevenueResult.IsSuccess.Should().BeTrue();
        var initialRevenue = initialRevenueResult.Value;

        var productStatistics = ProductStatistics.Create(
            productId,
            productName,
            viewCount,
            initialOrderCount,
            initialQuantitySold,
            initialRevenue);

        var saleQuantity = 2;
        var saleAmountResult = Money.FromDollars(100);
        saleAmountResult.IsSuccess.Should().BeTrue();
        var saleAmount = saleAmountResult.Value;

        // Act
        var updatedStatistics = productStatistics.AddSale(saleQuantity, saleAmount);

        // Assert
        updatedStatistics.OrderCount.Should().Be(initialOrderCount + 1);
        updatedStatistics.QuantitySold.Should().Be(initialQuantitySold + saleQuantity);
        updatedStatistics.Revenue.Amount.Should().Be(initialRevenue.Amount + saleAmount.Amount);
        updatedStatistics.ProductId.Should().Be(productId);
        updatedStatistics.ProductName.Should().Be(productName);
        updatedStatistics.ViewCount.Should().Be(viewCount);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productName = "Test Product";
        var viewCount = 100;
        var orderCount = 10;
        var quantitySold = 15;
        var revenueResult = Money.FromDollars(750);
        revenueResult.IsSuccess.Should().BeTrue();
        var revenue = revenueResult.Value;

        var statistics1 = ProductStatistics.Create(
            productId,
            productName,
            viewCount,
            orderCount,
            quantitySold,
            revenue);

        var statistics2 = ProductStatistics.Create(
            productId,
            productName,
            viewCount,
            orderCount,
            quantitySold,
            revenue);

        // Act & Assert
        statistics1.Should().Be(statistics2);
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productName = "Test Product";
        var viewCount = 100;
        var orderCount = 10;
        var quantitySold = 15;
        var revenueResult = Money.FromDollars(750);
        revenueResult.IsSuccess.Should().BeTrue();
        var revenue = revenueResult.Value;

        var statistics1 = ProductStatistics.Create(
            productId,
            productName,
            viewCount,
            orderCount,
            quantitySold,
            revenue);

        var statistics2 = ProductStatistics.Create(
            productId,
            productName,
            viewCount + 5, // Different view count
            orderCount,
            quantitySold,
            revenue);

        // Act & Assert
        statistics1.Should().NotBe(statistics2);
    }
}