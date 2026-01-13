namespace Shopilent.Domain.Sales.DTOs;

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public string ProductSlug { get; set; }
    public Guid? VariantId { get; set; }
    public string VariantSku { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string ImageUrl { get; set; }
    public List<CartItemVariantAttributeDto> VariantAttributes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}