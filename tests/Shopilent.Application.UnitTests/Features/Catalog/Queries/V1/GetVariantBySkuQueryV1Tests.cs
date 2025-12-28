using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetVariantBySku.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetVariantBySkuQueryV1Tests : TestBase
{
    private readonly GetVariantBySkuQueryHandlerV1 _handler;

    public GetVariantBySkuQueryV1Tests()
    {
        _handler = new GetVariantBySkuQueryHandlerV1(
            Fixture.MockProductVariantReadRepository.Object,
            Fixture.GetLogger<GetVariantBySkuQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidSku_ReturnsSuccessfulResult()
    {
        // Arrange
        var sku = "LAPTOP-001-16GB";
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var variantDto = new ProductVariantDto
        {
            Id = variantId,
            ProductId = productId,
            Sku = sku,
            Price = 1299.99m,
            Currency = "USD",
            StockQuantity = 15,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ReturnsAsync(variantDto);

        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.Sku.Should().Be(sku);
        result.Value.Price.Should().Be(1299.99m);
        result.Value.Currency.Should().Be("USD");
        result.Value.StockQuantity.Should().Be(15);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNonExistentSku_ReturnsNotFoundError()
    {
        // Arrange
        var sku = "NON-EXISTENT-SKU";

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ReturnsAsync((ProductVariantDto)null);

        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.ToString().Should().Be("NotFound");
        result.Error.Message.Should().Contain($"Product variant with SKU {sku} not found");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WithInvalidSku_ReturnsValidationError(string invalidSku)
    {
        // Arrange
        var query = new GetVariantBySkuQueryV1 { Sku = invalidSku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.ToString().Should().Be("Validation");
        result.Error.Message.Should().Be("SKU cannot be empty");

        // Verify repository was not called
        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var sku = "TEST-SKU";

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ThrowsAsync(new Exception("Database connection timeout"));

        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.GetBySkuFailed");
        result.Error.Message.Should().Contain("Database connection timeout");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var sku = "TEST-SKU-001";
        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ReturnsAsync(new ProductVariantDto { Id = Guid.NewGuid(), Sku = sku });

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        query.CacheKey.Should().Be($"variant-sku-{sku}");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Handle_WithCaseSensitiveSku_CallsRepositoryWithExactSku()
    {
        // Arrange
        var sku = "MiXeDcAsE-SKU-123";
        var variantDto = new ProductVariantDto
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Currency = "USD"
        };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ReturnsAsync(variantDto);

        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sku.Should().Be(sku);

        // Verify the exact SKU (with original casing) was passed to repository
        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetBySkuAsync(sku, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_VerifiesRepositoryCalledOnce()
    {
        // Arrange
        var sku = "VERIFY-ONCE-SKU";
        var variantDto = new ProductVariantDto { Id = Guid.NewGuid(), Sku = sku };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetBySkuAsync(sku, CancellationToken))
            .ReturnsAsync(variantDto);

        var query = new GetVariantBySkuQueryV1 { Sku = sku };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetBySkuAsync(sku, CancellationToken),
            Times.Once);
    }
}
