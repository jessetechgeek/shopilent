using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetProduct.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetProductQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetProductQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockProductReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetProductQueryHandlerV1>());

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
            cfg.RegisterServicesFromAssemblyContaining<GetProductQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var attributeId = Guid.NewGuid();

        var query = new GetProductQueryV1 { Id = productId };

        var productDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Test Product",
            Slug = "test-product",
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
            .Setup(repo => repo.GetDetailByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.Name.Should().Be("Test Product");
        result.Value.Slug.Should().Be("test-product");
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
            repo => repo.GetDetailByIdAsync(productId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetProduct_WithNonExistentId_ReturnsErrorResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductQueryV1 { Id = productId };

        // Mock repository calls - product not found
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(productId, CancellationToken))
            .ReturnsAsync((ProductDetailDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFound(productId).Code);

        // Verify repository was called correctly
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(productId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetProduct_WithInactiveProduct_ReturnsProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductQueryV1 { Id = productId };

        var inactiveProductDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Inactive Product",
            Slug = "inactive-product",
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
            .Setup(repo => repo.GetDetailByIdAsync(productId, CancellationToken))
            .ReturnsAsync(inactiveProductDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetProduct_WithComplexProductData_ReturnsAllData()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductQueryV1 { Id = productId };

        var complexProductDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Complex Product",
            Slug = "complex-product",
            Description = "A complex product with multiple images",
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
                    DisplayOrder = 1
                },
                new ProductImageDto
                {
                    ImageKey = "products/image2.jpg",
                    ThumbnailKey = "products/image2-thumb.jpg",
                    AltText = "Back view",
                    DisplayOrder = 2
                },
                new ProductImageDto
                {
                    ImageKey = "products/image3.jpg",
                    ThumbnailKey = "products/image3-thumb.jpg",
                    AltText = "Side view",
                    DisplayOrder = 3
                }
            },
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Categories = new List<CategoryDto>(),
            Attributes = new List<ProductAttributeDto>(),
            Variants = new List<ProductVariantDto>(),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
            LastModified = DateTime.UtcNow.AddDays(-5)
        };

        // Mock repository calls
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(productId, CancellationToken))
            .ReturnsAsync(complexProductDto);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(productId);
        result.Value.Name.Should().Be("Complex Product");
        result.Value.BasePrice.Should().Be(299.99m);

        // Verify metadata contains categories and attributes
        result.Value.Metadata.Should().NotBeNull();
        result.Value.Metadata.Should().ContainKey("categories");
        result.Value.Metadata.Should().ContainKey("attributes");

        // Verify multiple images in correct order
        result.Value.Images.Count.Should().Be(3);
        var orderedImages = result.Value.Images.OrderBy(i => i.DisplayOrder).ToList();
        orderedImages[0].AltText.Should().Be("Front view");
        orderedImages[1].AltText.Should().Be("Back view");
        orderedImages[2].AltText.Should().Be("Side view");
    }

    [Fact]
    public async Task GetProduct_CachesBehaviorCanBeVerified()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductQueryV1 { Id = productId };

        var productDto = new ProductDetailDto
        {
            Id = productId,
            Name = "Cached Product",
            Slug = "cached-product",
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
            .Setup(repo => repo.GetDetailByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        // Act - Call twice to verify caching behavior
        var result1 = await _mediator.Send(query, CancellationToken);
        var result2 = await _mediator.Send(query, CancellationToken);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Id.Should().Be(result2.Value.Id);

        // Note: The actual caching behavior depends on the ICachedQuery implementation
        // This test verifies the query can be called multiple times successfully
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(productId, CancellationToken),
            Times.Exactly(2)); // Without cache, repository is called each time
    }
}
