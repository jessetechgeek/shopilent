using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Domain.Catalog;

public class VariantAttribute : Entity
{
    private VariantAttribute()
    {
        // Required by EF Core
    }

    private VariantAttribute(Guid variantId, Guid attributeId, object value)
    {
        VariantId = variantId;
        AttributeId = attributeId;
        Value = new Dictionary<string, object> { { "value", value } };
    }

    // Add static factory method
    internal static VariantAttribute Create(Guid variantId, Guid attributeId, object value)
    {
        if (value == null)
            throw new ArgumentException("Value cannot be null", nameof(value));

        return new VariantAttribute(variantId, attributeId, value);
    }

    public Guid VariantId { get; private set; }
    public Guid AttributeId { get; private set; }
    public Dictionary<string, object> Value { get; private set; } = new();

    internal Result UpdateValue(object newValue)
    {
        if (newValue == null)
            return Result.Failure(AttributeErrors.InvalidConfigurationFormat);

        Value["value"] = newValue;
        return Result.Success();
    }
}
