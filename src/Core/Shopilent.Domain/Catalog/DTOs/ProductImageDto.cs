namespace Shopilent.Domain.Catalog.DTOs;

public class ProductImageDto
{
    public string ImageKey { get; set; }
    public string ThumbnailKey { get; set; }
    public string ImageUrl { get; set; }
    public string ThumbnailUrl { get; set; }
    public string AltText { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
}