using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Results;
using Shopilent.Infrastructure.Search.Meilisearch.Settings;
using System.Text.Json;
using Shopilent.Domain.Catalog.Repositories.Read;
using Index = Meilisearch.Index;

namespace Shopilent.Infrastructure.Search.Meilisearch.Services;

public class MeilisearchService : ISearchService
{
    private readonly MeilisearchClient _client;
    private readonly MeilisearchSettings _settings;
    private readonly ILogger<MeilisearchService> _logger;
    private readonly ICategoryReadRepository _categoryReadRepository;

    public MeilisearchService(
        IOptions<MeilisearchSettings> settings,
        ILogger<MeilisearchService> logger,
        ICategoryReadRepository categoryReadRepository
        )
    {
        _settings = settings.Value;
        _logger = logger;
        _categoryReadRepository = categoryReadRepository;
        _client = new MeilisearchClient(_settings.Url, _settings.ApiKey);
    }

    public async Task InitializeIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var index = _client.Index(_settings.Indexes.Products);

            await index.UpdateSearchableAttributesAsync(
                new[] { "name", "description", "sku", "variant_skus", "categories.name", "attributes.value", "attr-*" },
                cancellationToken);

            string[] currentFilterableAttrs = null;
            try
            {
                var attrs = await index.GetFilterableAttributesAsync(cancellationToken);
                currentFilterableAttrs = attrs?.ToArray() ?? [];
                _logger.LogDebug("Retrieved {Count} existing filterable attributes", currentFilterableAttrs.Length);
            }
            catch (Exception ex) when (ex.Message.Contains("index_not_found") ||
                                       ex.Message.Contains("Index") && ex.Message.Contains("not found"))
            {
                _logger.LogInformation("Index is newly created, using default filterable attributes");
                currentFilterableAttrs = [];
            }

            var systemFilterableAttrs = new HashSet<string>
            {
                "category_slugs",
                "price_range.min",
                "price_range.max",
                "has_stock",
                "is_active",
                "status"
            };

            foreach (var attr in currentFilterableAttrs ?? [])
            {
                systemFilterableAttrs.Add(attr);
            }

            var missingSystemAttrs = new[]
                {
                    "category_slugs", "price_range.min", "price_range.max", "has_stock", "is_active", "status"
                }
                .Where(attr => !currentFilterableAttrs?.Contains(attr) == true);

            if (missingSystemAttrs.Any() || currentFilterableAttrs?.Length == 0)
            {
                _logger.LogInformation("Updating filterable attributes: {Attributes}",
                    string.Join(", ", systemFilterableAttrs));
                await index.UpdateFilterableAttributesAsync(systemFilterableAttrs.ToArray(), cancellationToken);
            }

            await index.UpdateSortableAttributesAsync(
                new[] { "name", "base_price", "created_at", "updated_at", "total_stock" }, cancellationToken);

            await index.UpdateDisplayedAttributesAsync(new[] { "*" }, cancellationToken);

            _logger.LogInformation("Meilisearch indexes initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Meilisearch indexes");
            throw;
        }
    }

    public async Task<Domain.Common.Results.Result<IEnumerable<Guid>>> GetAllProductIdsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching all product IDs from search index...");
            var index = _client.Index(_settings.Indexes.Products);
            var allIds = new List<Guid>();

            const int limit = 1000;
            int offset = 0;
            bool hasMore = true;

            while (hasMore)
            {
                var searchParams = new SearchQuery
                {
                    Limit = limit, Offset = offset, AttributesToRetrieve = new[] { "id" }
                };

                var results = await index.SearchAsync<Dictionary<string, object>>("", searchParams, cancellationToken);

                if (results.Hits == null || !results.Hits.Any())
                {
                    hasMore = false;
                    break;
                }

                foreach (var hit in results.Hits)
                {
                    if (hit.TryGetValue("id", out var idValue))
                    {
                        if (idValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                        {
                            if (Guid.TryParse(jsonElement.GetString(), out var guid))
                            {
                                allIds.Add(guid);
                            }
                        }
                        else if (idValue is string idString && Guid.TryParse(idString, out var guid))
                        {
                            allIds.Add(guid);
                        }
                    }
                }

                offset += limit;
                hasMore = results.Hits.Count() == limit;
            }

            _logger.LogDebug("Retrieved {Count} product IDs from search index", allIds.Count);
            return Result.Success<IEnumerable<Guid>>(allIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch product IDs from search index");
            return Result.Failure<IEnumerable<Guid>>(
                Domain.Common.Errors.Error.Failure("Search.GetIdsFailed",
                    "Failed to fetch product IDs from search index"));
        }
    }

    public async Task<Result> DeleteProductsByIdsAsync(IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var idList = productIds.ToList();
            if (!idList.Any())
            {
                _logger.LogDebug("No product IDs provided for deletion");
                return Result.Success();
            }

            _logger.LogInformation("Deleting {Count} products from search index", idList.Count);
            var index = _client.Index(_settings.Indexes.Products);

            var idStrings = idList.Select(id => id.ToString()).ToArray();
            await index.DeleteDocumentsAsync(idStrings, cancellationToken);

            _logger.LogInformation("Successfully deleted {Count} products from search index", idList.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete products from search index");
            return Result.Failure(Domain.Common.Errors.Error.Failure("Search.BulkDeleteFailed",
                "Failed to delete products from search index"));
        }
    }

    public async Task<Result> IndexProductAsync(ProductSearchDocument document,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var index = _client.Index(_settings.Indexes.Products);
            var documentJson = JsonSerializer.Serialize(document,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            var documentDict = JsonSerializer.Deserialize<Dictionary<string, object>>(documentJson);

            var newAttributeNames = new List<string>();
            if (documentDict?.ContainsKey("flat_attributes") == true &&
                documentDict["flat_attributes"] is JsonElement flatAttributesElement)
            {
                if (flatAttributesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in flatAttributesElement.EnumerateObject())
                    {
                        newAttributeNames.Add(property.Name);

                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            var values = property.Value.EnumerateArray().Select(v => v.GetString())
                                .Where(v => !string.IsNullOrEmpty(v)).ToArray();
                            if (values.Length > 0)
                            {
                                documentDict[property.Name] = values;
                            }
                        }
                        else if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            var value = property.Value.GetString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                documentDict[property.Name] = new[] { value };
                            }
                        }
                    }
                }

                documentDict.Remove("flat_attributes");
            }

            if (newAttributeNames.Any())
            {
                await EnsureAttributesAreFilterableAsync(index, newAttributeNames);
            }

            await index.AddDocumentsAsync(new[] { documentDict }, "id", cancellationToken);

            _logger.LogDebug("Product {ProductId} indexed successfully", document.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index product {ProductId}", document.Id);
            return Result.Failure(Domain.Common.Errors.Error.Failure("Search.IndexFailed", "Failed to index product"));
        }
    }

    public async Task<Result> IndexProductsAsync(IEnumerable<ProductSearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var documentList = documents.ToList();
            if (!documentList.Any())
                return Result.Success();

            var index = _client.Index(_settings.Indexes.Products);
            var batches = documentList.Chunk(_settings.BatchSize);

            var allNewAttributeNames = new HashSet<string>();

            foreach (var batch in batches)
            {
                var documentDicts = new List<Dictionary<string, object>>();

                foreach (var doc in batch)
                {
                    var documentJson = JsonSerializer.Serialize(doc,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    var documentDict = JsonSerializer.Deserialize<Dictionary<string, object>>(documentJson);

                    if (documentDict?.ContainsKey("flat_attributes") == true &&
                        documentDict["flat_attributes"] is JsonElement flatAttributesElement)
                    {
                        if (flatAttributesElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in flatAttributesElement.EnumerateObject())
                            {
                                allNewAttributeNames.Add(property.Name);

                                if (property.Value.ValueKind == JsonValueKind.Array)
                                {
                                    var values = property.Value.EnumerateArray().Select(v => v.GetString())
                                        .Where(v => !string.IsNullOrEmpty(v)).ToArray();
                                    if (values.Length > 0)
                                    {
                                        documentDict[property.Name] = values;
                                    }
                                }
                                else if (property.Value.ValueKind == JsonValueKind.String)
                                {
                                    var value = property.Value.GetString();
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        documentDict[property.Name] = new[] { value };
                                    }
                                }
                            }
                        }

                        documentDict.Remove("flat_attributes");
                    }

                    if (documentDict != null)
                        documentDicts.Add(documentDict);
                }

                await index.AddDocumentsAsync(documentDicts.ToArray(), "id", cancellationToken);
            }

            if (allNewAttributeNames.Any())
            {
                await EnsureAttributesAreFilterableAsync(index, allNewAttributeNames);
            }

            _logger.LogInformation("Indexed {Count} products successfully", documentList.Count);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index products batch");
            return Result.Failure(
                Domain.Common.Errors.Error.Failure("Search.BatchIndexFailed", "Failed to index products batch"));
        }
    }

    public async Task<Result> DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var index = _client.Index(_settings.Indexes.Products);
            await index.DeleteOneDocumentAsync(productId.ToString());

            _logger.LogDebug("Product {ProductId} deleted from search index", productId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product {ProductId} from search index", productId);
            return Result.Failure(Domain.Common.Errors.Error.Failure("Search.DeleteFailed",
                "Failed to delete product from search index"));
        }
    }

    public async Task<Domain.Common.Results.Result<SearchResponse<ProductSearchResultDto>>>
        SearchProductsAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var index = _client.Index(_settings.Indexes.Products);
            var searchParams = new SearchQuery();
            searchParams.Limit = request.PageSize;
            searchParams.Offset = (request.PageNumber - 1) * request.PageSize;
            searchParams.Filter = BuildSearchFilters(request);
            searchParams.Sort = BuildSortParameters(request.SortBy, request.SortDescending);

            var facetsToAggregate = new List<string> { "category_slugs" };

            var currentFilterableAttrs = await index.GetFilterableAttributesAsync(cancellationToken);
            if (currentFilterableAttrs != null)
            {
                var attributeFacets = currentFilterableAttrs.Where(attr => attr.StartsWith("attr-")).ToArray();
                facetsToAggregate.AddRange(attributeFacets);
            }

            searchParams.Facets = facetsToAggregate.ToArray();

            var searchResult =
                await index.SearchAsync<Dictionary<string, object>>(request.Query, searchParams, cancellationToken);

            var items = searchResult.Hits.Select(hit => MapToProductSearchResult(hit)).ToArray();
            var facets = await MapFacetsAsync(searchResult.FacetDistribution, items, cancellationToken);

            // Cast to SearchResult to access EstimatedTotalHits property
            var totalHits = (searchResult as SearchResult<Dictionary<string, object>>)?.EstimatedTotalHits ?? 0;
            var response = new SearchResponse<ProductSearchResultDto>
            {
                Items = items,
                Facets = facets,
                TotalCount = totalHits,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling(totalHits / (double)request.PageSize),
                HasPreviousPage = request.PageNumber > 1,
                HasNextPage = request.PageNumber * request.PageSize < totalHits,
                Query = request.Query
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search products");
            return Result.Failure<SearchResponse<ProductSearchResultDto>>(
                Domain.Common.Errors.Error.Failure("Search.SearchFailed", "Failed to search products"));
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _client.HealthAsync();
            return health.Status == "available";
        }
        catch
        {
            return false;
        }
    }

    private string[] BuildSearchFilters(SearchRequest request)
    {
        var filters = new List<string>();

        if (request.ActiveOnly)
            filters.Add("is_active = true");

        if (request.CategorySlugs.Length > 0)
        {
            var categoryFilter =
                string.Join(" OR ", request.CategorySlugs.Select(slug => $"category_slugs = \"{slug}\""));
            filters.Add($"({categoryFilter})");
        }

        if (request.AttributeFilters.Any())
        {
            foreach (var (attributeName, values) in request.AttributeFilters)
            {
                if (values.Length > 0)
                {
                    var flatAttributeName = $"attr-{attributeName.ToLowerInvariant()}";
                    var attributeFilter = string.Join(" OR ", values.Select(value =>
                        $"{flatAttributeName} = \"{value}\""));
                    filters.Add($"({attributeFilter})");
                }
            }
        }

        if (request.PriceMin.HasValue)
            filters.Add($"price_range.max >= {request.PriceMin.Value}");

        if (request.PriceMax.HasValue)
            filters.Add($"price_range.min <= {request.PriceMax.Value}");

        if (request.InStockOnly)
            filters.Add("has_stock = true");

        return filters.ToArray();
    }

    private string[] BuildSortParameters(string sortBy, bool descending)
    {
        var sortField = sortBy.ToLowerInvariant() switch
        {
            "name" => "name",
            "price" => "base_price",
            "created" => "created_at",
            "updated" => "updated_at",
            "stock" => "total_stock",
            _ => null
        };

        if (sortField == null)
            return Array.Empty<string>();

        var direction = descending ? "desc" : "asc";
        return new[] { $"{sortField}:{direction}" };
    }

    private string MapSortColumn(string sortColumn)
    {
        return sortColumn.ToLowerInvariant() switch
        {
            "name" => "name",
            "baseprice" => "price",
            "createdat" => "created",
            "updatedat" => "updated",
            _ => "relevance"
        };
    }

    private ProductSearchResultDto MapToProductSearchResult(Dictionary<string, object> hit)
    {
        var json = JsonSerializer.Serialize(hit);
        var result = JsonSerializer.Deserialize<ProductSearchResultDto>(json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true
            });

        return result ?? new ProductSearchResultDto();
    }

    private ProductDto MapToProductDto(ProductSearchResultDto searchResult)
    {
        return new ProductDto
        {
            Id = searchResult.Id,
            Name = searchResult.Name,
            Description = searchResult.Description,
            Sku = searchResult.SKU,
            Slug = searchResult.Slug,
            BasePrice = searchResult.BasePrice,
            IsActive = searchResult.IsActive,
            CreatedAt = searchResult.CreatedAt,
            UpdatedAt = searchResult.UpdatedAt,
        };
    }

    private async Task<SearchFacets> MapFacetsAsync(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? facets, ProductSearchResultDto[] items,
        CancellationToken cancellationToken = default)
    {
        if (facets == null)
            return new SearchFacets();

        var categoryFacets = new List<CategoryFacet>();
        var attributeFacets = new List<AttributeFacet>();
        decimal priceMin = 0, priceMax = 0;

        var categorySlugs = new List<string>();
        foreach (var (facetName, facetValues) in facets)
        {
            if (facetName == "category_slugs")
            {
                foreach (var (categorySlug, _) in facetValues)
                {
                    if (!string.IsNullOrEmpty(categorySlug))
                    {
                        categorySlugs.Add(categorySlug);
                    }
                }
            }
        }

        var categoryLookup = new Dictionary<string, (Guid Id, string Name)>();
        if (categorySlugs.Any())
        {
            try
            {
                foreach (var slug in categorySlugs)
                {
                    var category = await _categoryReadRepository.GetBySlugAsync(slug, cancellationToken);
                    if (category != null)
                    {
                        categoryLookup[slug] = (category.Id, category.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to lookup category data for facets, using slugs");
            }
        }

        foreach (var (facetName, facetValues) in facets)
        {
            if (facetName == "category_slugs")
            {
                foreach (var (categorySlug, count) in facetValues)
                {
                    if (!string.IsNullOrEmpty(categorySlug))
                    {
                        var (id, name) = categoryLookup.TryGetValue(categorySlug, out var categoryData)
                            ? categoryData
                            : (Guid.Empty, categorySlug.Replace("-", " ").Replace("_", " "));

                        categoryFacets.Add(new CategoryFacet
                        {
                            Id = id, Name = name, Slug = categorySlug, Count = count
                        });
                    }
                }
            }
            else if (facetName.StartsWith("attr-"))
            {
                var attributeName = facetName.Substring(5);
                var attributeValueFacets = facetValues.Select(kv => new AttributeValueFacet
                {
                    Value = kv.Key, Count = kv.Value
                }).ToArray();

                if (attributeValueFacets.Length > 0)
                {
                    attributeFacets.Add(new AttributeFacet { Name = attributeName, Values = attributeValueFacets });
                }
            }
        }

        if (items.Length > 0)
        {
            var prices = items.Select(item => item.BasePrice).Where(price => price > 0).ToArray();
            if (prices.Length > 0)
            {
                priceMin = prices.Min();
                priceMax = prices.Max();
            }
        }

        return new SearchFacets
        {
            Categories = categoryFacets.ToArray(),
            Attributes = attributeFacets.ToArray(),
            PriceRange = new PriceRangeFacet { Min = priceMin, Max = priceMax }
        };
    }

    private async Task EnsureAttributesAreFilterableAsync(Index index, IEnumerable<string> attributeNames)
    {
        try
        {
            var currentFilterableAttrs = await index.GetFilterableAttributesAsync();
            var currentAttrsSet = new HashSet<string>(currentFilterableAttrs ?? []);
            var newAttrs = attributeNames.Where(attr => !currentAttrsSet.Contains(attr)).ToList();

            if (newAttrs.Any())
            {
                currentAttrsSet.UnionWith(newAttrs);
                await index.UpdateFilterableAttributesAsync(currentAttrsSet.ToArray());
                _logger.LogDebug("Added {Count} new filterable attributes: {Attributes}",
                    newAttrs.Count, string.Join(", ", newAttrs));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update filterable attributes for: {Attributes}",
                string.Join(", ", attributeNames));
        }
    }
}
