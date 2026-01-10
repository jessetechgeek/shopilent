using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Domain.Catalog;

public class ProductAttribute : Entity
{
    private ProductAttribute()
    {
        // Required by EF Core
    }

    private ProductAttribute(Guid productId, Guid attributeId, object value)
    {
        ProductId = productId;
        AttributeId = attributeId;
        Values = new Dictionary<string, object> { { "value", value } };
    }

    // Add static factory method
    internal static ProductAttribute Create(Guid productId, Guid attributeId, object value)
    {
        if (value == null)
            throw new ArgumentException("Value cannot be null", nameof(value));

        return new ProductAttribute(productId, attributeId, value);
    }

    public Guid ProductId { get; private set; }
    public Guid AttributeId { get; private set; }
    public Dictionary<string, object> Values { get; private set; } = new();

    internal Result UpdateValue(object value)
    {
        if (value == null)
            return Result.Failure(AttributeErrors.InvalidConfigurationFormat);

        Values["value"] = value;
        return Result.Success();
    }

    internal Result AddValue(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(AttributeErrors.InvalidConfigurationFormat);

        if (value == null)
            return Result.Failure(AttributeErrors.InvalidConfigurationFormat);

        Values[key] = value;
        return Result.Success();
    }
}
