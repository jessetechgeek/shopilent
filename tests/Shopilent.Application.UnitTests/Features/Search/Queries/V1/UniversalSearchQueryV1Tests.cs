using FluentAssertions;
using Moq;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Features.Search.Queries.UniversalSearch.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Search.Queries.V1;

public class UniversalSearchQueryV1Tests : TestBase
{
    private readonly UniversalSearchQueryHandlerV1 _handler;

    public UniversalSearchQueryV1Tests()
    {
        _handler = new UniversalSearchQueryHandlerV1(
            Fixture.MockSearchService.Object,
            Fixture.GetLogger<UniversalSearchQueryHandlerV1>(),
            Fixture.MockS3StorageService.Object);

        // Setup S3 service mock to return presigned URLs
        Fixture.MockS3StorageService
            .Setup(service => service.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan expiration, CancellationToken ct) =>
                Result.Success($"https://s3.example.com/{key}"));
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var query = new UniversalSearchQueryV1("test products");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product 1",
                    Description = "Test Description 1",
                    SKU = "SKU001",
                    Slug = "test-product-1",
                    BasePrice = 99.99m,
                    IsActive = true,
                    HasStock = true,
                    TotalStock = 10
                },
                new ProductSearchResultDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product 2",
                    Description = "Test Description 2",
                    SKU = "SKU002",
                    Slug = "test-product-2",
                    BasePrice = 149.99m,
                    IsActive = true,
                    HasStock = true,
                    TotalStock = 5
                }
            },
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 1,
            HasPreviousPage = false,
            HasNextPage = false,
            Query = "test products"
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Query.Should().Be("test products");
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_EmptyQuery_ReturnsSuccess()
    {
        // Arrange
        var query = new UniversalSearchQueryV1("");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 0,
            HasPreviousPage = false,
            HasNextPage = false,
            Query = ""
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCategoryFilters_PassesFiltersToSearchService()
    {
        // Arrange
        var categorySlugs = new[] { "electronics", "smartphones" };
        var query = new UniversalSearchQueryV1("phone", categorySlugs);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.CategorySlugs.Length == 2 &&
                    req.CategorySlugs.Contains("electronics") &&
                    req.CategorySlugs.Contains("smartphones")),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAttributeFilters_PassesFiltersToSearchService()
    {
        // Arrange
        var attributeFilters = new Dictionary<string, string[]>
        {
            { "color", new[] { "red", "blue" } },
            { "size", new[] { "large" } }
        };
        var query = new UniversalSearchQueryV1("shirt", Array.Empty<string>(), attributeFilters);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.AttributeFilters.ContainsKey("color") &&
                    req.AttributeFilters["color"].Contains("red") &&
                    req.AttributeFilters["color"].Contains("blue") &&
                    req.AttributeFilters.ContainsKey("size") &&
                    req.AttributeFilters["size"].Contains("large")),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPriceRange_PassesPriceRangeToSearchService()
    {
        // Arrange
        var query = new UniversalSearchQueryV1(
            Query: "product",
            PriceMin: 50.0m,
            PriceMax: 200.0m);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.PriceMin == 50.0m &&
                    req.PriceMax == 200.0m),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithStockAndActiveFilters_PassesFiltersToSearchService()
    {
        // Arrange
        var query = new UniversalSearchQueryV1(
            Query: "product",
            InStockOnly: true,
            ActiveOnly: false);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.InStockOnly == true &&
                    req.ActiveOnly == false),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPagination_PassesPaginationToSearchService()
    {
        // Arrange
        var query = new UniversalSearchQueryV1(
            Query: "product",
            PageNumber: 2,
            PageSize: 10);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 2,
            PageSize = 10
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.PageNumber == 2 &&
                    req.PageSize == 10),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithSorting_PassesSortingToSearchService()
    {
        // Arrange
        var query = new UniversalSearchQueryV1(
            Query: "product",
            SortBy: "price",
            SortDescending: true);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.SortBy == "price" &&
                    req.SortDescending == true),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SearchServiceFailure_ReturnsFailureResult()
    {
        // Arrange
        var query = new UniversalSearchQueryV1("test");
        
        var error = Domain.Common.Errors.Error.Failure("Search.Failed", "Search operation failed");
        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Failure<SearchResponse<ProductSearchResultDto>>(error));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Search.Failed");
        result.Error.Message.Should().Be("Search operation failed");
    }

    [Fact]
    public async Task Handle_SearchServiceThrowsException_ReturnsUnexpectedErrorResult()
    {
        // Arrange
        var query = new UniversalSearchQueryV1("test");

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Search.UnexpectedError");
        result.Error.Message.Should().Be("An unexpected error occurred during search");
    }

    [Fact]
    public async Task Handle_DefaultParameters_UsesCorrectDefaults()
    {
        // Arrange
        var query = new UniversalSearchQueryV1();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.Query == "" &&
                    req.CategorySlugs.Length == 0 &&
                    req.AttributeFilters.Count == 0 &&
                    req.PriceMin == null &&
                    req.PriceMax == null &&
                    req.InStockOnly == false &&
                    req.ActiveOnly == true &&
                    req.PageNumber == 1 &&
                    req.PageSize == 20 &&
                    req.SortBy == "relevance" &&
                    req.SortDescending == false),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ComplexQuery_MapsAllParametersCorrectly()
    {
        // Arrange
        var categoryFilters = new[] { "electronics", "computers" };
        var attributeFilters = new Dictionary<string, string[]>
        {
            { "brand", new[] { "apple", "dell" } },
            { "color", new[] { "silver" } }
        };

        var query = new UniversalSearchQueryV1(
            Query: "laptop computer",
            CategorySlugs: categoryFilters,
            AttributeFilters: attributeFilters,
            PriceMin: 500m,
            PriceMax: 2000m,
            InStockOnly: true,
            ActiveOnly: true,
            PageNumber: 3,
            PageSize: 15,
            SortBy: "price",
            SortDescending: false);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 3,
            PageSize = 15
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        Fixture.MockSearchService.Verify(
            service => service.SearchProductsAsync(
                It.Is<SearchRequest>(req => 
                    req.Query == "laptop computer" &&
                    req.CategorySlugs.SequenceEqual(categoryFilters) &&
                    req.AttributeFilters["brand"].SequenceEqual(new[] { "apple", "dell" }) &&
                    req.AttributeFilters["color"].SequenceEqual(new[] { "silver" }) &&
                    req.PriceMin == 500m &&
                    req.PriceMax == 2000m &&
                    req.InStockOnly == true &&
                    req.ActiveOnly == true &&
                    req.PageNumber == 3 &&
                    req.PageSize == 15 &&
                    req.SortBy == "price" &&
                    req.SortDescending == false),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithFacets_ReturnsFacetsInResponse()
    {
        // Arrange
        var query = new UniversalSearchQueryV1("test");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20,
            Facets = new SearchFacets
            {
                Categories = new[]
                {
                    new CategoryFacet { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics", Count = 5 },
                    new CategoryFacet { Id = Guid.NewGuid(), Name = "Clothing", Slug = "clothing", Count = 3 }
                },
                Attributes = new[]
                {
                    new AttributeFacet
                    {
                        Name = "color",
                        Values = new[]
                        {
                            new AttributeValueFacet { Value = "red", Count = 10 },
                            new AttributeValueFacet { Value = "blue", Count = 7 }
                        }
                    }
                },
                PriceRange = new PriceRangeFacet { Min = 10.99m, Max = 999.99m }
            }
        };

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Facets.Categories.Should().HaveCount(2);
        result.Value.Facets.Attributes.Should().HaveCount(1);
        result.Value.Facets.PriceRange.Min.Should().Be(10.99m);
        result.Value.Facets.PriceRange.Max.Should().Be(999.99m);
    }
}