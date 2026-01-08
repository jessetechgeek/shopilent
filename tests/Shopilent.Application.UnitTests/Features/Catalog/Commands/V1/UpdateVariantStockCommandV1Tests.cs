using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateVariantStock.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateVariantStockCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create a ProductVariant with a specific ID for testing
    /// </summary>
    private static ProductVariant CreateProductVariantWithId(Guid variantId, Guid productId, string sku, decimal price, int stockQuantity)
    {
        var money = Money.Create(price, "USD").Value;
        var variant = ProductVariant.Create(productId, sku, money, stockQuantity).Value;
        var idProperty = typeof(ProductVariant).GetProperty("Id");
        idProperty?.SetValue(variant, variantId);
        return variant;
    }

    public UpdateVariantStockCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateVariantStockCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateVariantStockCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateVariantStockCommandV1>, UpdateVariantStockCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidStockUpdate_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var newStockQuantity = 50;
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = newStockQuantity
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(variantId);
        result.Value.StockQuantity.Should().Be(newStockQuantity);
        result.Value.IsActive.Should().Be(variant.IsActive);
        variant.StockQuantity.Should().Be(newStockQuantity);

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ZeroStockQuantity_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = 0
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(0);
        variant.StockQuantity.Should().Be(0);

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LargeStockQuantity_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var largeStockQuantity = 999999;
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = largeStockQuantity
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(largeStockQuantity);
        variant.StockQuantity.Should().Be(largeStockQuantity);

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentVariantId_ReturnsNotFoundError()
    {
        // Arrange
        var nonExistentVariantId = Guid.NewGuid();
        var command = new UpdateVariantStockCommandV1
        {
            Id = nonExistentVariantId,
            StockQuantity = 25
        };

        // Mock that variant doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentVariantId, CancellationToken))
            .ReturnsAsync((ProductVariant)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Variant.NotFound");
        result.Error.Message.Should().Contain($"Variant with ID {nonExistentVariantId} not found");

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NegativeStockQuantity_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var negativeStock = -10;
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = negativeStock
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The specific error would depend on the SetStockQuantity validation logic

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var newStockQuantity = 30;
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = newStockQuantity
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Don't set authenticated user (simulate anonymous operation)

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(newStockQuantity);

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = 25
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock save entities to throw exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Variant.UpdateStockFailed");
    }

    [Fact]
    public async Task Handle_VariantGetByIdError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = 25
        };

        // Mock variant retrieval to throw exception
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ThrowsAsync(new Exception("Database timeout"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Variant.UpdateStockFailed");
    }

    [Fact]
    public async Task Handle_StockUpdateSameValue_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var currentStock = 10;
        var command = new UpdateVariantStockCommandV1
        {
            Id = variantId,
            StockQuantity = currentStock // Same as current stock
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, currentStock);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(currentStock);

        // Verify changes were saved (even if stock didn't change)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
