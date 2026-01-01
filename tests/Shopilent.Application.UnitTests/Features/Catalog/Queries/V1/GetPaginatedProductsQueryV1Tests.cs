using FluentAssertions;
using Moq;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Features.Catalog.Queries.GetPaginatedProducts.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetPaginatedProductsQueryV1Tests : TestBase
{
    private readonly GetPaginatedProductsQueryHandlerV1 _handler;
    private readonly Mock<ISearchService> _mockSearchService;

    public GetPaginatedProductsQueryV1Tests()
    {
        _mockSearchService = new Mock<ISearchService>();
        _handler = new GetPaginatedProductsQueryHandlerV1(
            _mockSearchService.Object,
            Fixture.GetLogger<GetPaginatedProductsQueryHandlerV1>(),
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
    public async Task Handle_WithValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "test",
            IsActiveOnly = true,
            SortColumn = "Name",
            SortDescending = false
        };

        var products = new ProductSearchResultDto[]
        {
            new ProductSearchResultDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Product 1",
                Slug = "test-product-1",
                BasePrice = 29.99m,
                IsActive = true,
                HasStock = true,
                TotalStock = 50
            },
            new ProductSearchResultDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Product 2",
                Slug = "test-product-2",
                BasePrice = 49.99m,
                IsActive = true,
                HasStock = false,
                TotalStock = 0
            }
        };

        var searchResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = products,
            TotalCount = 25,
            PageNumber = 1,
            PageSize = 10,
            TotalPages = 3
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ReturnsAsync(Result.Success(searchResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(25);
        result.Value.Items.Length.Should().Be(2);
        result.Value.Items.First().Name.Should().Be("Test Product 1");
        
        _mockSearchService.Verify(
            service => service.SearchProductsAsync(It.Is<SearchRequest>(r =>
                r.Query == "test" &&
                r.ActiveOnly == true &&
                r.PageNumber == 1 &&
                r.PageSize == 10 &&
                r.SortBy == "name" &&
                r.SortDescending == false
            ), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyQuery_ReturnsAllProducts()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            PageNumber = 1,
            PageSize = 20,
            SearchQuery = "",
            IsActiveOnly = false
        };

        var searchResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new ProductSearchResultDto[0],
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ReturnsAsync(Result.Success(searchResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Length.Should().Be(0);

        _mockSearchService.Verify(
            service => service.SearchProductsAsync(It.Is<SearchRequest>(r => 
                r.Query == "" &&
                r.ActiveOnly == false
            ), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithFiltersAndPriceRange_PassesCorrectParameters()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            PageNumber = 2,
            PageSize = 5,
            SearchQuery = "electronics",
            CategorySlugs = new[] { "smartphones", "laptops" },
            AttributeFilters = new Dictionary<string, string[]>
            {
                { "brand", new[] { "apple", "samsung" } },
                { "color", new[] { "black", "white" } }
            },
            PriceMin = 100,
            PriceMax = 1000,
            InStockOnly = true,
            IsActiveOnly = true,
            SortColumn = "BasePrice",
            SortDescending = true
        };

        var searchResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new ProductSearchResultDto[0],
            TotalCount = 50,
            PageNumber = 2,
            PageSize = 5,
            TotalPages = 10
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ReturnsAsync(Result.Success(searchResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockSearchService.Verify(
            service => service.SearchProductsAsync(It.Is<SearchRequest>(r =>
                r.Query == "electronics" &&
                r.CategorySlugs.SequenceEqual(new[] { "smartphones", "laptops" }) &&
                r.AttributeFilters["brand"].SequenceEqual(new[] { "apple", "samsung" }) &&
                r.AttributeFilters["color"].SequenceEqual(new[] { "black", "white" }) &&
                r.PriceMin == 100 &&
                r.PriceMax == 1000 &&
                r.InStockOnly == true &&
                r.ActiveOnly == true &&
                r.PageNumber == 2 &&
                r.PageSize == 5 &&
                r.SortBy == "price" &&
                r.SortDescending == true
            ), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSearchServiceReturnsFailure_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            SearchQuery = "test"
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ReturnsAsync(Result.Failure<SearchResponse<ProductSearchResultDto>>(
                Error.Failure("Search.Failed", "Search service error")));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Search.Failed");
        result.Error.Message.Should().Be("Search service error");
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            SearchQuery = "test"
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ThrowsAsync(new Exception("Search service exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Products.GetPaginatedFailed");
        result.Error.Message.Should().Contain("Search service exception");
    }

    [Theory]
    [InlineData("Name", "name")]
    [InlineData("BasePrice", "price")]
    [InlineData("CreatedAt", "created")]
    [InlineData("UpdatedAt", "updated")]
    [InlineData("unknown", "name")]
    [InlineData("", "name")]
    public async Task Handle_VerifiesSortColumnMapping(string inputColumn, string expectedColumn)
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            SortColumn = inputColumn
        };

        var searchResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new ProductSearchResultDto[0],
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 10,
            TotalPages = 0
        };

        _mockSearchService
            .Setup(service => service.SearchProductsAsync(It.IsAny<SearchRequest>(), CancellationToken))
            .ReturnsAsync(Result.Success(searchResponse));

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        _mockSearchService.Verify(
            service => service.SearchProductsAsync(It.Is<SearchRequest>(r => 
                r.SortBy == expectedColumn
            ), CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var query = new GetPaginatedProductsQueryV1
        {
            PageNumber = 1,
            PageSize = 10,
            SearchQuery = "test",
            CategorySlugs = new[] { "electronics" },
            PriceMin = 50,
            PriceMax = 200,
            InStockOnly = true
        };

        // Act - Cache properties are read-only and set during construction
        // We verify they have expected values

        // Assert
        query.CacheKey.Should().NotBeEmpty();
        query.CacheKey.Should().Contain("products-page-1");
        query.CacheKey.Should().Contain("size-10");
        query.CacheKey.Should().Contain("electronics");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }
}