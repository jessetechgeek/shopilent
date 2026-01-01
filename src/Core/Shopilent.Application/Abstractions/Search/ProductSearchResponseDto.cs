namespace Shopilent.Application.Abstractions.Search;

/// <summary>
/// Response DTO for product search results with presigned image URLs
/// </summary>
public class ProductSearchResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string SKU { get; init; } = "";
    public string Slug { get; init; } = "";
    public decimal BasePrice { get; init; }
    public ProductSearchCategory[] Categories { get; init; } = [];
    public ProductSearchAttribute[] Attributes { get; init; } = [];
    public ProductSearchVariantResponseDto[] Variants { get; init; } = [];
    public ProductSearchImageResponseDto[] Images { get; init; } = [];
    public string Status { get; init; } = "";
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public ProductSearchPriceRange PriceRange { get; init; } = new();
    public bool HasStock { get; init; }
    public int TotalStock { get; init; }
}

/// <summary>
/// Response DTO for product variant with presigned image URLs
/// </summary>
public class ProductSearchVariantResponseDto
{
    public Guid Id { get; init; }
    public string SKU { get; init; } = "";
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
    public ProductSearchVariantAttribute[] Attributes { get; init; } = [];
    public ProductSearchImageResponseDto[] Images { get; init; } = [];
}

/// <summary>
/// Response DTO for product images with presigned URLs instead of S3 keys
/// </summary>
public class ProductSearchImageResponseDto
{
    public string ImageUrl { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
    public string AltText { get; init; } = "";
    public bool IsDefault { get; init; }
    public int Order { get; init; }
}
