using Shopilent.Domain.Common;

namespace Shopilent.Domain.Catalog;

public class ProductCategory : Entity
{
    private ProductCategory()
    {
        // Required by EF Core
    }

    private ProductCategory(Guid productId, Guid categoryId)
    {
        ProductId = productId;
        CategoryId = categoryId;
    }

    // Add static factory method
    internal static ProductCategory Create(Guid productId, Guid categoryId)
    {
        return new ProductCategory(productId, categoryId);
    }

    public Guid ProductId { get; private set; }
    public Guid CategoryId { get; private set; }
}
