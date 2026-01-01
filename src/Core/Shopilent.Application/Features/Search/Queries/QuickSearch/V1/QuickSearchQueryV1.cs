using Shopilent.Application.Abstractions.Caching;
using Shopilent.Application.Abstractions.Messaging;

namespace Shopilent.Application.Features.Search.Queries.QuickSearch.V1;

public record QuickSearchQueryV1(
    string Query,
    int Limit = 5
) : ICachedQuery<QuickSearchResponseV1>
{
    public string CacheKey => $"quick-search:{Query.ToLowerInvariant()}:{Limit}";

    public TimeSpan? Expiration => TimeSpan.FromMinutes(5);
}
