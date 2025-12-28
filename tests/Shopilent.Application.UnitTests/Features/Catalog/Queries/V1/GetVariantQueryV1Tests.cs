using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetVariant.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetVariantQueryV1Tests : TestBase
{
    private readonly GetVariantQueryHandlerV1 _handler;

    public GetVariantQueryV1Tests()
    {
        _handler = new GetVariantQueryHandlerV1(
            Fixture.MockProductVariantReadRepository.Object,
            Fixture.GetLogger<GetVariantQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidVariantId_ReturnsSuccessfulResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantDto = new ProductVariantDto
        {
            Id = variantId,
            ProductId = productId,
            Sku = "TEST-001-L",
            Price = 129.99m,
            Currency = "USD",
            StockQuantity = 25,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variantDto);

        var query = new GetVariantQueryV1 { Id = variantId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.Sku.Should().Be("TEST-001-L");
        result.Value.Price.Should().Be(129.99m);
        result.Value.Currency.Should().Be("USD");
        result.Value.StockQuantity.Should().Be(25);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithInvalidVariantId_ReturnsNotFoundError()
    {
        // Arrange
        var variantId = Guid.NewGuid();

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync((ProductVariantDto)null);

        var query = new GetVariantQueryV1 { Id = variantId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.ToString().Should().Be("NotFound");
        result.Error.Message.Should().Contain($"Product variant with ID {variantId} not found");
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        var query = new GetVariantQueryV1 { Id = variantId };

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.GetFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var query = new GetVariantQueryV1 { Id = variantId };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(new ProductVariantDto { Id = variantId, Sku = "TEST-001" });

        // Act
        await _handler.Handle(query, CancellationToken);

        // Assert
        query.CacheKey.Should().Be($"variant-{variantId}");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Handle_VerifiesRepositoryCalledOnce()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var query = new GetVariantQueryV1 { Id = variantId };

        var variantDto = new ProductVariantDto { Id = variantId, Sku = "TEST-001" };

        Fixture.MockProductVariantReadRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variantDto);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        Fixture.MockProductVariantReadRepository.Verify(
            repo => repo.GetByIdAsync(variantId, CancellationToken),
            Times.Once);
    }
}
