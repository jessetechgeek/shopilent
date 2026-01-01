namespace Shopilent.API.Endpoints.Search.QuickSearch.V1;

public class QuickSearchResponseV1
{
    public ProductSearchSuggestionDto[] Suggestions { get; init; } = [];
    public int TotalCount { get; init; }
    public string Query { get; init; } = "";
}

public class ProductSearchSuggestionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
    public decimal BasePrice { get; init; }
}
