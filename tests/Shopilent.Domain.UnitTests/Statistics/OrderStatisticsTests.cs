using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Statistics;

namespace Shopilent.Domain.Tests.Statistics;

public class OrderStatisticsTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateOrderStatistics()
    {
        // Arrange
        var period = new DateTime(2023, 1, 1);
        var orderCount = 10;
        var totalRevenueResult = Money.FromDollars(1000);
        totalRevenueResult.IsSuccess.Should().BeTrue();
        var totalRevenue = totalRevenueResult.Value;
        var newCustomerCount = 6;
        var returnCustomerCount = 4;

        // Act
        var orderStatistics = OrderStatistics.Create(
            period,
            orderCount,
            totalRevenue,
            newCustomerCount,
            returnCustomerCount);

        // Assert
        orderStatistics.Period.Should().Be(period);
        orderStatistics.OrderCount.Should().Be(orderCount);
        orderStatistics.TotalRevenue.Should().Be(totalRevenue);
        orderStatistics.NewCustomerCount.Should().Be(newCustomerCount);
        orderStatistics.ReturnCustomerCount.Should().Be(returnCustomerCount);
        
        // Check calculated properties
        var expectedAvg = 100m; // 1000 / 10
        orderStatistics.AverageOrderValue.Amount.Should().Be(expectedAvg);
        orderStatistics.AverageOrderValue.Currency.Should().Be(totalRevenue.Currency);
        
        var expectedRate = 40m; // (4 / 10) * 100
        orderStatistics.ReturnCustomerRate.Should().Be(expectedRate);
    }

    [Fact]
    public void Create_WithZeroOrders_ShouldHandleAverageAndRateCorrectly()
    {
        // Arrange
        var period = new DateTime(2023, 1, 1);
        var orderCount = 0;
        var totalRevenueResult = Money.FromDollars(0);
        totalRevenueResult.IsSuccess.Should().BeTrue();
        var totalRevenue = totalRevenueResult.Value;
        var newCustomerCount = 0;
        var returnCustomerCount = 0;

        // Act
        var orderStatistics = OrderStatistics.Create(
            period,
            orderCount,
            totalRevenue,
            newCustomerCount,
            returnCustomerCount);

        // Assert
        orderStatistics.Period.Should().Be(period);
        orderStatistics.OrderCount.Should().Be(orderCount);
        orderStatistics.TotalRevenue.Should().Be(totalRevenue);
        
        // Check edge cases with zero orders
        orderStatistics.AverageOrderValue.Amount.Should().Be(0m);
        orderStatistics.AverageOrderValue.Currency.Should().Be(totalRevenue.Currency);
        orderStatistics.ReturnCustomerRate.Should().Be(0m);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var period = new DateTime(2023, 1, 1);
        var orderCount = 10;
        var totalRevenueResult = Money.FromDollars(1000);
        totalRevenueResult.IsSuccess.Should().BeTrue();
        var totalRevenue = totalRevenueResult.Value;
        var newCustomerCount = 6;
        var returnCustomerCount = 4;

        var statistics1 = OrderStatistics.Create(
            period,
            orderCount,
            totalRevenue,
            newCustomerCount,
            returnCustomerCount);

        var statistics2 = OrderStatistics.Create(
            period,
            orderCount,
            totalRevenue,
            newCustomerCount,
            returnCustomerCount);

        // Act & Assert
        statistics1.Should().Be(statistics2);
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var period = new DateTime(2023, 1, 1);
        var totalRevenueResult = Money.FromDollars(1000);
        totalRevenueResult.IsSuccess.Should().BeTrue();
        var totalRevenue = totalRevenueResult.Value;

        var statistics1 = OrderStatistics.Create(
            period,
            10,
            totalRevenue,
            6,
            4);

        var statistics2 = OrderStatistics.Create(
            period,
            15, // Different order count
            totalRevenue,
            6,
            4);

        // Act & Assert
        statistics1.Should().NotBe(statistics2);
    }
}