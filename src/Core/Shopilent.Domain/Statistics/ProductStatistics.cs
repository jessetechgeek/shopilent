using Shopilent.Domain.Common;
using Shopilent.Domain.Common.ValueObjects;

namespace Shopilent.Domain.Statistics;

public class ProductStatistics : ValueObject
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int ViewCount { get; private set; }
    public int OrderCount { get; private set; }
    public int QuantitySold { get; private set; }
    public Money Revenue { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private ProductStatistics(
        Guid productId,
        string productName,
        int viewCount,
        int orderCount,
        int quantitySold,
        Money revenue)
    {
        ProductId = productId;
        ProductName = productName;
        ViewCount = viewCount;
        OrderCount = orderCount;
        QuantitySold = quantitySold;
        Revenue = revenue;
        LastUpdated = DateTime.UtcNow;
    }

    public static ProductStatistics Create(
        Guid productId,
        string productName,
        int viewCount,
        int orderCount,
        int quantitySold,
        Money revenue)
    {
        return new ProductStatistics(
            productId,
            productName,
            viewCount,
            orderCount,
            quantitySold,
            revenue);
    }

    public ProductStatistics IncrementViews()
    {
        return new ProductStatistics(
            ProductId,
            ProductName,
            ViewCount + 1,
            OrderCount,
            QuantitySold,
            Revenue);
    }

    public ProductStatistics AddSale(int quantity, Money saleAmount)
    {
        return new ProductStatistics(
            ProductId,
            ProductName,
            ViewCount,
            OrderCount + 1,
            QuantitySold + quantity,
            Revenue.Add(saleAmount));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ProductId;
        yield return ProductName;
        yield return ViewCount;
        yield return OrderCount;
        yield return QuantitySold;
        yield return Revenue;
    }
}
