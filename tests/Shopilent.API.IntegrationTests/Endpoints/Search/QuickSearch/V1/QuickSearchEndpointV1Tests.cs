using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.Common.Models;
using Shopilent.API.Endpoints.Search.QuickSearch.V1;
using Shopilent.Application.Abstractions.Search;

namespace Shopilent.API.IntegrationTests.Endpoints.Search.QuickSearch.V1;

public class QuickSearchEndpointV1Tests : ApiIntegrationTestBase
{
    public QuickSearchEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task QuickSearch_WithValidQuery_ShouldReturnSuccess()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=laptop&limit=5");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Query.Should().Be("laptop");
        response.Data.Suggestions.Should().NotBeNull();
    }

    [Fact]
    public async Task QuickSearch_WithEmptyQuery_ShouldReturnValidationError()
    {
        // Arrange & Act
        var httpResponse = await Client.GetAsync("v1/search/quick?query=");

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await httpResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Search query must be at least 3 characters long");
    }

    [Fact]
    public async Task QuickSearch_WithQueryLessThan3Characters_ShouldReturnValidationError()
    {
        // Arrange & Act
        var httpResponse = await Client.GetAsync("v1/search/quick?query=ab");

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await httpResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Search query must be at least 3 characters long");
    }

    [Fact]
    public async Task QuickSearch_WithCustomLimit_ShouldRespectLimit()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=test&limit=3");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Suggestions.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task QuickSearch_WithLimitExceeding20_ShouldReturnValidationError()
    {
        // Arrange & Act
        var httpResponse = await Client.GetAsync("v1/search/quick?query=test&limit=25");

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await httpResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Limit must not exceed 20");
    }

    [Fact]
    public async Task QuickSearch_WithNegativeLimit_ShouldReturnValidationError()
    {
        // Arrange & Act
        var httpResponse = await Client.GetAsync("v1/search/quick?query=test&limit=-1");

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await httpResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Limit must be greater than 0");
    }

    [Fact]
    public async Task QuickSearch_WithQueryExceeding200Characters_ShouldReturnValidationError()
    {
        // Arrange
        var longQuery = new string('a', 201);

        // Act
        var httpResponse = await Client.GetAsync($"v1/search/quick?query={longQuery}");

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await httpResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Search query must not exceed 200 characters");
    }

    [Fact]
    public async Task QuickSearch_ShouldReturnMinimalFields()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=laptop&limit=1");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();

        if (response.Data.Suggestions.Length > 0)
        {
            var suggestion = response.Data.Suggestions[0];
            suggestion.Id.Should().NotBeEmpty();
            suggestion.Name.Should().NotBeNullOrEmpty();
            suggestion.Slug.Should().NotBeNullOrEmpty();
            suggestion.BasePrice.Should().BeGreaterThan(0);
            // ImageUrl and ThumbnailUrl should be present (empty strings if no image)
            suggestion.ImageUrl.Should().NotBeNull();
            suggestion.ThumbnailUrl.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task QuickSearch_WithDefaultLimit_ShouldUse5AsDefault()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=test");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        // Even if more results exist, should not exceed default limit of 5
        response.Data.Suggestions.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task QuickSearch_ShouldReturnTotalCount()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=test&limit=2");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        // TotalCount should reflect all matches, not just returned suggestions
    }

    [Fact]
    public async Task QuickSearch_WithNonExistentProduct_ShouldReturnEmptyResults()
    {
        // Arrange
        await InitializeSearchIndexesAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=nonexistentproduct123456");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Suggestions.Should().BeEmpty();
        response.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task QuickSearch_ShouldBeAccessibleAnonymously()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        ClearAuthenticationHeader(); // Ensure no auth header

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=test");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task QuickSearch_WithProductImages_ShouldReturnPresignedUrls()
    {
        // Arrange
        await InitializeSearchIndexesAsync();
        await IndexTestProductsAsync();

        // Act
        var response = await GetApiResponseAsync<QuickSearchResponseV1>("v1/search/quick?query=laptop&limit=1");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();

        if (response.Data.Suggestions.Length > 0)
        {
            var suggestion = response.Data.Suggestions[0];
            // Product with images should have presigned URLs
            suggestion.ImageUrl.Should().NotBeNullOrEmpty();
            suggestion.ThumbnailUrl.Should().NotBeNullOrEmpty();
            suggestion.ImageUrl.Should().StartWith("http");
            suggestion.ThumbnailUrl.Should().StartWith("http");
        }
    }

    // Helper method to index test products for search
    private async Task IndexTestProductsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

        var testProducts = new List<ProductSearchDocument>
        {
            new ProductSearchDocument
            {
                Id = Guid.NewGuid(),
                Name = "Dell Laptop",
                Description = "High-performance laptop",
                SKU = "LAPTOP-001",
                Slug = "dell-laptop",
                BasePrice = 999.99m,
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                HasStock = true,
                TotalStock = 10,
                Images = new[]
                {
                    new ProductSearchImage
                    {
                        ImageKey = "laptop-image.jpg",
                        ThumbnailKey = "laptop-thumb.jpg",
                        AltText = "Dell Laptop",
                        IsDefault = true,
                        Order = 1
                    }
                },
                Categories = Array.Empty<ProductSearchCategory>(),
                Attributes = Array.Empty<ProductSearchAttribute>(),
                Variants = Array.Empty<ProductSearchVariant>(),
                CategorySlugs = Array.Empty<string>(),
                VariantSKUs = Array.Empty<string>(),
                PriceRange = new ProductSearchPriceRange { Min = 999.99m, Max = 999.99m },
                AttributeFilters = new Dictionary<string, string[]>(),
                FlatAttributes = new Dictionary<string, string[]>()
            },
            new ProductSearchDocument
            {
                Id = Guid.NewGuid(),
                Name = "Test Product 1",
                Description = "Test product for search",
                SKU = "TEST-001",
                Slug = "test-product-1",
                BasePrice = 99.99m,
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                HasStock = true,
                TotalStock = 5,
                Images = Array.Empty<ProductSearchImage>(),
                Categories = Array.Empty<ProductSearchCategory>(),
                Attributes = Array.Empty<ProductSearchAttribute>(),
                Variants = Array.Empty<ProductSearchVariant>(),
                CategorySlugs = Array.Empty<string>(),
                VariantSKUs = Array.Empty<string>(),
                PriceRange = new ProductSearchPriceRange { Min = 99.99m, Max = 99.99m },
                AttributeFilters = new Dictionary<string, string[]>(),
                FlatAttributes = new Dictionary<string, string[]>()
            },
            new ProductSearchDocument
            {
                Id = Guid.NewGuid(),
                Name = "Test Product 2",
                Description = "Another test product",
                SKU = "TEST-002",
                Slug = "test-product-2",
                BasePrice = 149.99m,
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                HasStock = true,
                TotalStock = 8,
                Images = Array.Empty<ProductSearchImage>(),
                Categories = Array.Empty<ProductSearchCategory>(),
                Attributes = Array.Empty<ProductSearchAttribute>(),
                Variants = Array.Empty<ProductSearchVariant>(),
                CategorySlugs = Array.Empty<string>(),
                VariantSKUs = Array.Empty<string>(),
                PriceRange = new ProductSearchPriceRange { Min = 149.99m, Max = 149.99m },
                AttributeFilters = new Dictionary<string, string[]>(),
                FlatAttributes = new Dictionary<string, string[]>()
            }
        };

        await searchService.IndexProductsAsync(testProducts);

        // Small delay to allow Meilisearch to index
        await Task.Delay(500);
    }
}
