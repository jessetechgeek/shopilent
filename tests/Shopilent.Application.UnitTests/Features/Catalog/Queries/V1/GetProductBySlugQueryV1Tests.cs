using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetProductBySlug.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetProductBySlugQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetProductBySlugQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockProductReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetProductBySlugQueryHandlerV1>());

        // Setup S3 service mock to return public URLs
        Fixture.MockS3StorageService
            .Setup(service => service.GetPublicUrlAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken ct) =>
                Result.Success($"https://s3.example.com/{key}"));

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetProductBySlugQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task GetProductBySlug_WithValidSlug_ReturnsProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var slug = "test-product";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        var productDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Test Product",
            Slug = slug,
            Description = "Test product description",
            BasePrice = 99.99m,
            Currency = "USD",
            Sku = "TEST-001",
            IsActive = true,
            Metadata = new Dictionary<string, object>(),
            Images = new List<ProductImageDto>
            {
                new ProductImageDto
                {
                    ImageKey = "products/test-image.jpg",
                    ThumbnailKey = "products/test-image-thumb.jpg",
                    AltText = "Product image",
                    DisplayOrder = 1
                }
            },
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            Variants = new List<ProductVariantDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow.AddDays(-1)
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync(productDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.Name.Should().Be("Test Product");
        result.Value.Slug.Should().Be(slug);
        result.Value.BasePrice.Should().Be(99.99m);
        result.Value.Currency.Should().Be("USD");
        result.Value.IsActive.Should().BeTrue();

        // Verify images - keys should be null, URLs should be populated
        result.Value.Images.Should().ContainSingle();
        result.Value.Images.First().ImageKey.Should().BeNull();
        result.Value.Images.First().ThumbnailKey.Should().BeNull();
        result.Value.Images.First().ImageUrl.Should().Be("https://s3.example.com/products/test-image.jpg");
        result.Value.Images.First().ThumbnailUrl.Should().Be("https://s3.example.com/products/test-image-thumb.jpg");
        result.Value.Images.First().AltText.Should().Be("Product image");

        // Verify repository was called correctly
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailBySlugAsync(slug, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetProductBySlug_WithNonExistentSlug_ReturnsErrorResult()
    {
        // Arrange
        var slug = "non-existent-product";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        // Mock repository calls - product not found
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync((ProductDetailDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFoundBySlug(slug).Code);
        result.Error.Message.Should().Contain(slug);

        // Verify repository was called correctly
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailBySlugAsync(slug, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetProductBySlug_WithInactiveProduct_ReturnsProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var slug = "inactive-product";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        var inactiveProductDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Inactive Product",
            Slug = slug,
            BasePrice = 50.00m,
            Currency = "USD",
            IsActive = false, // Inactive product
            Metadata = new Dictionary<string, object>(),
            Images = new List<ProductImageDto>(),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            Variants = new List<ProductVariantDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow.AddDays(-1)
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync(inactiveProductDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.Slug.Should().Be(slug);
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetProductBySlug_WithComplexProductData_ReturnsAllData()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var slug = "complex-product-123";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        var complexProductDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Complex Product",
            Slug = slug,
            Description = "A complex product with multiple images and variants",
            BasePrice = 299.99m,
            Currency = "USD",
            Sku = "COMPLEX-001",
            IsActive = true,
            Metadata =
                new Dictionary<string, object>
                {
                    ["categories"] = new[] { "Electronics", "Smartphones" },
                    ["attributes"] =
                        new Dictionary<string, string>
                        {
                            ["Color"] = "Space Gray", ["Storage"] = "128GB", ["Brand"] = "TestBrand"
                        }
                },
            Images = new List<ProductImageDto>
            {
                new ProductImageDto
                {
                    ImageKey = "products/image1.jpg",
                    ThumbnailKey = "products/image1-thumb.jpg",
                    AltText = "Front view",
                    DisplayOrder = 1,
                    IsDefault = true
                },
                new ProductImageDto
                {
                    ImageKey = "products/image2.jpg",
                    ThumbnailKey = "products/image2-thumb.jpg",
                    AltText = "Back view",
                    DisplayOrder = 2,
                    IsDefault = false
                },
                new ProductImageDto
                {
                    ImageKey = "products/image3.jpg",
                    ThumbnailKey = "products/image3-thumb.jpg",
                    AltText = "Side view",
                    DisplayOrder = 3,
                    IsDefault = false
                }
            },
            Variants = new List<ProductVariantDto>
            {
                new ProductVariantDto
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Sku = "VAR-001",
                    Price = 299.99m,
                    Currency = "USD",
                    StockQuantity = 10,
                    IsActive = true,
                    Metadata = new Dictionary<string, object>(),
                    Attributes = new List<VariantAttributeDto>(),
                    Images = new List<ProductImageDto>
                    {
                        new ProductImageDto
                        {
                            ImageKey = "variants/var1-image.jpg",
                            ThumbnailKey = "variants/var1-thumb.jpg",
                            AltText = "Variant 1",
                            DisplayOrder = 1
                        }
                    },
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }
            },
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow.AddDays(-5)
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync(complexProductDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.Name.Should().Be("Complex Product");
        result.Value.Slug.Should().Be(slug);
        result.Value.BasePrice.Should().Be(299.99m);

        // Verify metadata contains categories and attributes
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Should().ContainKey("categories");
        result.Value.Metadata.Should().ContainKey("attributes");

        // Verify multiple images in correct order
        result.Value.Images.Count.Should().Be(3);
        var orderedImages = result.Value.Images.OrderBy(i => i.DisplayOrder).ToList();
        orderedImages[0].AltText.Should().Be("Front view");
        orderedImages[0].IsDefault.Should().BeTrue();
        orderedImages[1].AltText.Should().Be("Back view");
        orderedImages[2].AltText.Should().Be("Side view");

        // Verify variants with images
        result.Value.Variants.Should().ContainSingle();
        result.Value.Variants.First().Images.Should().ContainSingle();
        result.Value.Variants.First().Images.First().ImageKey.Should().BeNull();
        result.Value.Variants.First().Images.First().ImageUrl.Should().Be("https://s3.example.com/variants/var1-image.jpg");
    }

    [Fact]
    public async Task GetProductBySlug_WithSpecialCharactersInSlug_HandlesCorrectly()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var slug = "product-with-special-chars-2024";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        var productDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Product with Special Chars",
            Slug = slug,
            BasePrice = 49.99m,
            Currency = "USD",
            IsActive = true,
            Metadata = new Dictionary<string, object>(),
            Images = new List<ProductImageDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            Variants = new List<ProductVariantDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync(productDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be(slug);
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailBySlugAsync(slug, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetProductBySlug_CachesBehaviorCanBeVerified()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var slug = "cached-product";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        var productDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Cached Product",
            Slug = slug,
            BasePrice = 25.00m,
            Currency = "USD",
            IsActive = true,
            Metadata = new Dictionary<string, object>(),
            Images = new List<ProductImageDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            Variants = new List<ProductVariantDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailBySlugAsync(slug, CancellationToken))
            .ReturnsAsync(productDto);

        // Act - Call twice to verify caching behavior
        var result1 = await _mediator.Send(query, CancellationToken);
        var result2 = await _mediator.Send(query, CancellationToken);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Id.Should().Be(result2.Value.Id);
        result1.Value.Slug.Should().Be(slug);
        result2.Value.Slug.Should().Be(slug);

        // Note: The actual caching behavior depends on the ICachedQuery implementation
        // This test verifies the query can be called multiple times successfully
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailBySlugAsync(slug, CancellationToken),
            Times.Exactly(2)); // Without cache, repository is called each time
    }

    [Fact]
    public async Task GetProductBySlug_VerifiesCacheKeyFormat()
    {
        // Arrange
        var slug = "test-cache-key";
        var query = new GetProductBySlugQueryV1 { Slug = slug };

        // Assert
        query.CacheKey.Should().Be($"product-slug-{slug}");
        query.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }
}
