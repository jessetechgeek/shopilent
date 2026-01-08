using Shopilent.Domain.Common;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Statistics;

public class OrderStatistics : ValueObject
{
    public DateTime Period { get; private set; }
    public int OrderCount { get; private set; }
    public Money TotalRevenue { get; private set; }
    public Money AverageOrderValue { get; private set; }
    public int NewCustomerCount { get; private set; }
    public int ReturnCustomerCount { get; private set; }
    public decimal ReturnCustomerRate => OrderCount > 0 ? (decimal)ReturnCustomerCount / OrderCount * 100 : 0;

    private OrderStatistics(
        DateTime period,
        int orderCount,
        Money totalRevenue,
        Money averageOrderValue,
        int newCustomerCount,
        int returnCustomerCount)
    {
        Period = period;
        OrderCount = orderCount;
        TotalRevenue = totalRevenue;
        AverageOrderValue = averageOrderValue;
        NewCustomerCount = newCustomerCount;
        ReturnCustomerCount = returnCustomerCount;
    }

    public static OrderStatistics Create(
        DateTime period,
        int orderCount,
        Money totalRevenue,
        int newCustomerCount,
        int returnCustomerCount)
    {
        var averageOrderValue = orderCount > 0
            ? Money.Create(totalRevenue.Amount / orderCount, totalRevenue.Currency)
            : Money.Zero(totalRevenue.Currency);

        return new OrderStatistics(
            period,
            orderCount,
            totalRevenue,
            averageOrderValue,
            newCustomerCount,
            returnCustomerCount);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Period;
        yield return OrderCount;
        yield return TotalRevenue;
        yield return AverageOrderValue;
        yield return NewCustomerCount;
        yield return ReturnCustomerCount;
    }
}
