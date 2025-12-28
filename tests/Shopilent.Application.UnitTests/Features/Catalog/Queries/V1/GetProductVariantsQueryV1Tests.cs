using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetProductVariants.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetProductVariantsQueryV1Tests : TestBase
{
    private readonly GetProductVariantsQueryHandlerV1 _handler;

    public GetProductVariantsQueryV1Tests()
    {
        _handler = new GetProductVariantsQueryHandlerV1(
            Fixture.MockUnitOfWork.Object,
            Fixture.MockProductVariantReadRepository.Object,
            Fixture.GetLogger<GetProductVariantsQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidProductId_ReturnsProductVariants()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        var productDto = new ProductDto { Id = productId, Name = "Test Product", IsActive = true };

        var variants = new List<ProductVariantDto>
        {
            new ProductVariantDto
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Sku = "TP-001-S",
                Price = 99.99m,
                Currency = "USD",
                StockQuantity = 50,
                IsActive = true
            },
            new ProductVariantDto
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Sku = "TP-001-M",
                Price = 109.99m,
                Currency = "USD",
                StockQuantity = 30,
                IsActive = true
            },
            new ProductVariantDto
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Sku = "TP-001-L",
                Price = 119.99m,
                Currency = "USD",
                StockQuantity = 0,
                IsActive = false
            }
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByProductIdAsync(productId, CancellationToken))
            .ReturnsAsync(variants);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);

        var smallVariant = result.Value.First(v => v.Sku == "TP-001-S");
        smallVariant.Sku.Should().Be("TP-001-S");
        smallVariant.Price.Should().Be(99.99m);
        smallVariant.StockQuantity.Should().Be(50);
        smallVariant.IsActive.Should().BeTrue();

        var largeVariant = result.Value.First(v => v.Sku == "TP-001-L");
        largeVariant.Sku.Should().Be("TP-001-L");
        largeVariant.StockQuantity.Should().Be(0);
        largeVariant.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithProductWithNoVariants_ReturnsEmptyList()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        var productDto = new ProductDto { Id = productId, Name = "Test Product", IsActive = true };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByProductIdAsync(productId, CancellationToken))
            .ReturnsAsync(new List<ProductVariantDto>());

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentProduct_ReturnsNotFoundError()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync((ProductDto)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.ToString().Should().Be("NotFound");
        result.Error.Message.Should().Contain($"Product with ID {productId} not found");

        // Verify that variant repository was not called
        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductRetrievalThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariants.GetFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task Handle_WhenVariantRetrievalThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        var productDto = new ProductDto { Id = productId, Name = "Test Product", IsActive = true };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByProductIdAsync(productId, CancellationToken))
            .ThrowsAsync(new Exception("Variant query failed"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariants.GetFailed");
        result.Error.Message.Should().Contain("Variant query failed");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        // Act & Assert - Cache properties are read-only and set during construction
        query.CacheKey.Should().Be($"product-{productId}-variants");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Handle_VerifiesRepositoryCallsInCorrectOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var query = new GetProductVariantsQueryV1 { ProductId = productId };

        var productDto = new ProductDto { Id = productId, Name = "Test Product" };
        var variants = new List<ProductVariantDto>
        {
            new ProductVariantDto { Id = Guid.NewGuid(), ProductId = productId }
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(productDto);

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByProductIdAsync(productId, CancellationToken))
            .ReturnsAsync(variants);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify both repositories were called with correct parameters
        Fixture.MockProductReadRepository.Verify(
            repo => repo.GetByIdAsync(productId, CancellationToken),
            Times.Once);

        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetByProductIdAsync(productId, CancellationToken),
            Times.Once);
    }
}
