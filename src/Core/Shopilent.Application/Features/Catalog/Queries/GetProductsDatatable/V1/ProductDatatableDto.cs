using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.Features.Catalog.Queries.GetProductsDatatable.V1;

public sealed class ProductDatatableDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Slug { get; set; }
    public string Description { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; }
    public string Sku { get; set; }
    public bool IsActive { get; set; }
    public int VariantsCount { get; set; }
    public int TotalStockQuantity { get; set; }
    public List<string> Categories { get; set; } = new List<string>();
    public IReadOnlyList<ProductImageDto> Images { get; set; } = new List<ProductImageDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}