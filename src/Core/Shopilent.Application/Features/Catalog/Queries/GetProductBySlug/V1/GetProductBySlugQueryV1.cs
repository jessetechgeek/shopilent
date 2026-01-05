using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.Features.Catalog.Queries.GetProductBySlug.V1;

public sealed record GetProductBySlugQueryV1 : IQuery<ProductDetailDto>, ICachedQuery<ProductDetailDto>
{
    public string Slug { get; init; }

    public string CacheKey => $"product-slug-{Slug}";
    public TimeSpan? Expiration => TimeSpan.FromMinutes(30);
}
