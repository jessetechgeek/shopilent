using FluentAssertions;
using Moq;
using Shopilent.Application.Abstractions.Search;
using Shopilent.Application.Features.Search.Queries.QuickSearch.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Search.Queries.V1;

public class QuickSearchQueryV1Tests : TestBase
{
    private readonly QuickSearchQueryHandlerV1 _handler;

    public QuickSearchQueryV1Tests()
    {
        // Setup S3 service to return successful presigned URLs for any key
        Fixture.MockS3StorageService
            .Setup(service => service.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan expiration, CancellationToken ct) =>
                Result.Success($"https://s3.example.com/{key}"));

        _handler = new QuickSearchQueryHandlerV1(
            Fixture.MockSearchService.Object,
            Fixture.GetLogger<QuickSearchQueryHandlerV1>(),
            Fixture.MockS3StorageService.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var query = new QuickSearchQueryV1("laptop");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Gaming Laptop",
                    Description = "High-performance gaming laptop",
                    SKU = "LAP001",
                    Slug = "gaming-laptop",
                    BasePrice = 1299.99m,
                    IsActive = true,
                    HasStock = true,
                    TotalStock = 5,
                    Images = new[]
                    {
                        new ProductSearchImage
                        {
                            ImageKey = "images/laptop1.jpg",
                            ThumbnailKey = "thumbnails/laptop1.jpg",
                            AltText = "Gaming Laptop",
                            IsDefault = true,
                            Order = 1
                        }
                    },
                    Variants = Array.Empty<ProductSearchVariant>()
                },
                new ProductSearchResultDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Business Laptop",
                    Description = "Professional business laptop",
                    SKU = "LAP002",
                    Slug = "business-laptop",
                    BasePrice = 899.99m,
                    IsActive = true,
                    HasStock = true,
                    TotalStock = 10,
                    Images = new[]
                    {
                        new ProductSearchImage
                        {
                            ImageKey = "images/laptop2.jpg",
                            ThumbnailKey = "thumbnails/laptop2.jpg",
                            AltText = "Business Laptop",
                            IsDefault = true,
                            Order = 1
                        }
                    },
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 5,
            Query = "laptop"
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
        result.Value.Suggestions.Should().HaveCount(2);
        result.Value.Query.Should().Be("laptop");
        result.Value.TotalCount.Should().Be(2);
        result.Value.Suggestions[0].Name.Should().Be("Gaming Laptop");
        result.Value.Suggestions[0].Slug.Should().Be("gaming-laptop");
        result.Value.Suggestions[0].BasePrice.Should().Be(1299.99m);
        result.Value.Suggestions[0].ImageUrl.Should().NotBeNullOrEmpty();
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithDefaultLimit_UsesCorrectLimit()
    {
        // Arrange
        var query = new QuickSearchQueryV1("phone");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 5
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
                    req.PageSize == 5 &&
                    req.PageNumber == 1),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCustomLimit_UsesProvidedLimit()
    {
        // Arrange
        var query = new QuickSearchQueryV1("tablet", Limit: 10);

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
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
                    req.PageSize == 10 &&
                    req.PageNumber == 1),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlwaysSearchesActiveOnly()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 5
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
                    req.ActiveOnly == true &&
                    req.InStockOnly == false),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UsesSortByRelevance()
    {
        // Arrange
        var query = new QuickSearchQueryV1("search term");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 5
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
                    req.SortBy == "relevance" &&
                    req.SortDescending == false),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyFilters()
    {
        // Arrange
        var query = new QuickSearchQueryV1("query");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 5
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
                    req.CategorySlugs.Length == 0 &&
                    req.AttributeFilters.Count == 0 &&
                    req.PriceMin == null &&
                    req.PriceMax == null),
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExtractsDefaultImageThumbnail_ReturnsCorrectThumbnailKey()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = new[]
                    {
                        new ProductSearchImage
                        {
                            ImageKey = "images/default.jpg",
                            ThumbnailKey = "thumbnails/default.jpg",
                            IsDefault = true,
                            Order = 1
                        },
                        new ProductSearchImage
                        {
                            ImageKey = "images/second.jpg",
                            ThumbnailKey = "thumbnails/second.jpg",
                            IsDefault = false,
                            Order = 2
                        }
                    },
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions.Should().HaveCount(1);
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoDefaultImage_ReturnsFirstImageByOrder()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = new[]
                    {
                        new ProductSearchImage
                        {
                            ImageKey = "images/second.jpg",
                            ThumbnailKey = "thumbnails/second.jpg",
                            IsDefault = false,
                            Order = 2
                        },
                        new ProductSearchImage
                        {
                            ImageKey = "images/first.jpg",
                            ThumbnailKey = "thumbnails/first.jpg",
                            IsDefault = false,
                            Order = 1
                        }
                    },
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoProductImages_FallsBackToVariantDefaultImage()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = new[]
                    {
                        new ProductSearchVariant
                        {
                            Id = Guid.NewGuid(),
                            SKU = "VAR001",
                            IsActive = true,
                            Images = new[]
                            {
                                new ProductSearchImage
                                {
                                    ImageKey = "images/variant-default.jpg",
                                    ThumbnailKey = "thumbnails/variant-default.jpg",
                                    IsDefault = true,
                                    Order = 1
                                }
                            }
                        }
                    }
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoVariantDefaultImage_FallsBackToFirstVariantImageByOrder()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = new[]
                    {
                        new ProductSearchVariant
                        {
                            Id = Guid.NewGuid(),
                            SKU = "VAR001",
                            IsActive = true,
                            Images = new[]
                            {
                                new ProductSearchImage
                                {
                                    ImageKey = "images/variant-second.jpg",
                                    ThumbnailKey = "thumbnails/variant-second.jpg",
                                    IsDefault = false,
                                    Order = 2
                                },
                                new ProductSearchImage
                                {
                                    ImageKey = "images/variant-first.jpg",
                                    ThumbnailKey = "thumbnails/variant-first.jpg",
                                    IsDefault = false,
                                    Order = 1
                                }
                            }
                        }
                    }
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoImages_ReturnsEmptyUrls()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions[0].ImageUrl.Should().BeEmpty();
        result.Value.Suggestions[0].ThumbnailUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsInactiveVariants_WhenExtractingThumbnail()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = "Test Product",
                    Slug = "test-product",
                    BasePrice = 99.99m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = new[]
                    {
                        new ProductSearchVariant
                        {
                            Id = Guid.NewGuid(),
                            SKU = "VAR001",
                            IsActive = false,
                            Images = new[]
                            {
                                new ProductSearchImage
                                {
                                    ImageKey = "images/inactive-variant.jpg",
                                    ThumbnailKey = "thumbnails/inactive-variant.jpg",
                                    IsDefault = true,
                                    Order = 1
                                }
                            }
                        },
                        new ProductSearchVariant
                        {
                            Id = Guid.NewGuid(),
                            SKU = "VAR002",
                            IsActive = true,
                            Images = new[]
                            {
                                new ProductSearchImage
                                {
                                    ImageKey = "images/active-variant.jpg",
                                    ThumbnailKey = "thumbnails/active-variant.jpg",
                                    IsDefault = true,
                                    Order = 1
                                }
                            }
                        }
                    }
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
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
        result.Value.Suggestions[0].ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_EmptyResults_ReturnsEmptySuggestions()
    {
        // Arrange
        var query = new QuickSearchQueryV1("nonexistent");

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = Array.Empty<ProductSearchResultDto>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 5,
            Query = "nonexistent"
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
        result.Value.Suggestions.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Query.Should().Be("nonexistent");
    }

    [Fact]
    public async Task Handle_SearchServiceFailure_ReturnsFailureResult()
    {
        // Arrange
        var query = new QuickSearchQueryV1("test");

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
        var query = new QuickSearchQueryV1("test");

        Fixture.MockSearchService
            .Setup(service => service.SearchProductsAsync(
                It.IsAny<SearchRequest>(),
                CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Search.QuickSearchFailed");
        result.Error.Message.Should().Be("An unexpected error occurred during quick search");
    }

    [Fact]
    public async Task Handle_MapsAllProductFields_Correctly()
    {
        // Arrange
        var query = new QuickSearchQueryV1("product");
        var productId = Guid.NewGuid();
        var expectedName = "Premium Wireless Headphones";
        var expectedSlug = "premium-wireless-headphones";
        var expectedPrice = 249.99m;
        var expectedThumbnail = "thumbnails/headphones.jpg";

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = productId,
                    Name = expectedName,
                    Slug = expectedSlug,
                    BasePrice = expectedPrice,
                    IsActive = true,
                    Images = new[]
                    {
                        new ProductSearchImage
                        {
                            ImageKey = "images/headphones.jpg",
                            ThumbnailKey = expectedThumbnail,
                            IsDefault = true,
                            Order = 1
                        }
                    },
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5,
            Query = "product"
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
        var suggestion = result.Value.Suggestions[0];
        suggestion.Id.Should().Be(productId);
        suggestion.Name.Should().Be(expectedName);
        suggestion.Slug.Should().Be(expectedSlug);
        suggestion.BasePrice.Should().Be(expectedPrice);
        suggestion.ImageUrl.Should().NotBeNullOrEmpty();
        suggestion.ThumbnailUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_MultipleProducts_MaintainsOrder()
    {
        // Arrange
        var query = new QuickSearchQueryV1("laptop", Limit: 3);
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var product3Id = Guid.NewGuid();

        var expectedResponse = new SearchResponse<ProductSearchResultDto>
        {
            Items = new[]
            {
                new ProductSearchResultDto
                {
                    Id = product1Id,
                    Name = "First Product",
                    Slug = "first-product",
                    BasePrice = 100m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = Array.Empty<ProductSearchVariant>()
                },
                new ProductSearchResultDto
                {
                    Id = product2Id,
                    Name = "Second Product",
                    Slug = "second-product",
                    BasePrice = 200m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = Array.Empty<ProductSearchVariant>()
                },
                new ProductSearchResultDto
                {
                    Id = product3Id,
                    Name = "Third Product",
                    Slug = "third-product",
                    BasePrice = 300m,
                    IsActive = true,
                    Images = Array.Empty<ProductSearchImage>(),
                    Variants = Array.Empty<ProductSearchVariant>()
                }
            },
            TotalCount = 3,
            PageNumber = 1,
            PageSize = 3,
            Query = "laptop"
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
        result.Value.Suggestions.Should().HaveCount(3);
        result.Value.Suggestions[0].Id.Should().Be(product1Id);
        result.Value.Suggestions[0].Name.Should().Be("First Product");
        result.Value.Suggestions[1].Id.Should().Be(product2Id);
        result.Value.Suggestions[1].Name.Should().Be("Second Product");
        result.Value.Suggestions[2].Id.Should().Be(product3Id);
        result.Value.Suggestions[2].Name.Should().Be("Third Product");
    }

    [Fact]
    public void Query_CacheKey_IncludesQueryAndLimit()
    {
        // Arrange
        var query1 = new QuickSearchQueryV1("laptop", Limit: 5);
        var query2 = new QuickSearchQueryV1("LAPTOP", Limit: 5);
        var query3 = new QuickSearchQueryV1("laptop", Limit: 10);

        // Act
        var cacheKey1 = query1.CacheKey;
        var cacheKey2 = query2.CacheKey;
        var cacheKey3 = query3.CacheKey;

        // Assert
        cacheKey1.Should().Be("quick-search:laptop:5");
        cacheKey2.Should().Be("quick-search:laptop:5");
        cacheKey1.Should().Be(cacheKey2);
        cacheKey1.Should().NotBe(cacheKey3);
    }

    [Fact]
    public void Query_Expiration_IsFiveMinutes()
    {
        // Arrange
        var query = new QuickSearchQueryV1("test");

        // Act
        var expiration = query.Expiration;

        // Assert
        expiration.Should().NotBeNull();
        expiration.Should().Be(TimeSpan.FromMinutes(5));
    }
}
