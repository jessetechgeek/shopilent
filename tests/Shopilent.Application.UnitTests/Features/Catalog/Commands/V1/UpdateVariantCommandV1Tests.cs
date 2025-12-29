using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateVariant.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateVariantCommandV1Tests : TestBase
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

    public UpdateVariantCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockS3StorageService.Object);
        services.AddTransient(sp => Fixture.MockImageService.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateVariantCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateVariantCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateVariantCommandV1>, UpdateVariantCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidUpdateRequest_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            Sku = "UPDATED-SKU-001",
            Price = 149.99m,
            StockQuantity = 25,
            IsActive = true,
            Metadata = new Dictionary<string, object> { { "color", "blue" }, { "size", "large" } }
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "OLD-SKU-001", 99.99m, 10);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Mock SKU uniqueness check
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(command.Sku, variantId, CancellationToken))
            .ReturnsAsync(false);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.Sku.Should().Be(command.Sku);
        result.Value.Price.Should().Be(command.Price);
        result.Value.StockQuantity.Should().Be(command.StockQuantity);
        result.Value.IsActive.Should().Be(command.IsActive.Value);

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
        var command = new UpdateVariantCommandV1
        {
            Id = nonExistentVariantId,
            Sku = "NEW-SKU-001",
            Price = 99.99m
        };

        // Mock that variant doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentVariantId, CancellationToken))
            .ReturnsAsync((ProductVariant)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.NotFound");
        result.Error.Message.Should().Contain($"Product variant with ID {nonExistentVariantId} not found");

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ReturnsConflictError()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var duplicateSku = "EXISTING-SKU-001";

        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            Sku = duplicateSku,
            Price = 149.99m
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "OLD-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Mock SKU already exists
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.SkuExistsAsync(duplicateSku, variantId, CancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.DuplicateSku");
        result.Error.Message.Should().Contain($"A product variant with SKU '{duplicateSku}' already exists");

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OnlyPriceUpdate_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            Price = 199.99m
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Price.Should().Be(command.Price);
        result.Value.Sku.Should().Be("EXISTING-SKU-001"); // SKU should remain unchanged

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnlyStockQuantityUpdate_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            StockQuantity = 50
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(command.StockQuantity);

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ActivateVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            IsActive = true
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Deactivate the variant first
        existingVariant.Deactivate();

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivateVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            IsActive = false
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidPrice_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            Price = -10.00m // Invalid negative price
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The specific error would depend on the Money.Create validation logic

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new UpdateVariantCommandV1
        {
            Id = variantId,
            Price = 149.99m
        };

        var existingVariant = CreateProductVariantWithId(variantId, productId, "EXISTING-SKU-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(existingVariant);

        // Mock save entities to throw exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.UpdateFailed");
        result.Error.Message.Should().Contain("Failed to update product variant");
    }
}
