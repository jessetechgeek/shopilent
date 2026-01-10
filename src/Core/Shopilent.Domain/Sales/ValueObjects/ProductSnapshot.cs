using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Domain.Sales.ValueObjects;

public class ProductSnapshot
{
    public string Name { get; }
    public string Sku { get; }
    public string Slug { get; }
    public string VariantSku { get; }
    public Dictionary<string, object> VariantAttributes { get; }

    private ProductSnapshot(
        string name,
        string sku,
        string slug,
        string variantSku = null,
        Dictionary<string, object> variantAttributes = null)
    {
        Name = name;
        Sku = sku;
        Slug = slug;
        VariantSku = variantSku;
        VariantAttributes = variantAttributes;
    }

    public static Result<ProductSnapshot> Create(
        string name,
        string sku = null,
        string slug = null,
        string variantSku = null,
        Dictionary<string, object> variantAttributes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ProductSnapshot>(ProductSnapshotErrors.NameRequired);

        // SKU is optional - allow null/empty
        return Result.Success(new ProductSnapshot(name, sku, slug, variantSku, variantAttributes));
    }

    // Convert to dictionary for storage
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            { "name", Name }
        };

        if (!string.IsNullOrWhiteSpace(Sku))
            dict["sku"] = Sku;

        if (!string.IsNullOrWhiteSpace(Slug))
            dict["slug"] = Slug;

        if (!string.IsNullOrWhiteSpace(VariantSku))
            dict["variant_sku"] = VariantSku;

        if (VariantAttributes != null && VariantAttributes.Any())
            dict["variant_attributes"] = VariantAttributes;

        return dict;
    }

    // Create from dictionary (for hydration from database)
    public static Result<ProductSnapshot> FromDictionary(Dictionary<string, object> dict)
    {
        if (dict == null)
            return Result.Failure<ProductSnapshot>(ProductSnapshotErrors.InvalidData);

        var name = dict.ContainsKey("name") ? dict["name"]?.ToString() : null;
        var sku = dict.ContainsKey("sku") ? dict["sku"]?.ToString() : null;
        var slug = dict.ContainsKey("slug") ? dict["slug"]?.ToString() : null;
        var variantSku = dict.ContainsKey("variant_sku") ? dict["variant_sku"]?.ToString() : null;

        Dictionary<string, object> variantAttributes = null;
        if (dict.ContainsKey("variant_attributes"))
        {
            variantAttributes = dict["variant_attributes"] as Dictionary<string, object>;
        }

        return Create(name, sku, slug, variantSku, variantAttributes);
    }
}
