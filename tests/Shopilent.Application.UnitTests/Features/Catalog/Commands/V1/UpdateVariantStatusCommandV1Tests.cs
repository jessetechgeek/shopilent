using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateVariantStatus.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateVariantStatusCommandV1Tests : TestBase
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

    public UpdateVariantStatusCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateVariantStatusCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateVariantStatusCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateVariantStatusCommandV1>, UpdateVariantStatusCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ActivateVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = true
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Deactivate the variant first to test activation
        variant.Deactivate();

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
        variant.IsActive.Should().BeTrue();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivateVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = false
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
        variant.IsActive.Should().BeFalse();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentVariantId_ReturnsNotFoundError()
    {
        // Arrange
        var nonExistentVariantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = nonExistentVariantId,
            IsActive = true
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
        result.Error.Message.Should().Contain($"Variant with ID {nonExistentVariantId} was not found");

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyActiveVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = true
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Variant is active by default, so no need to change status

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
        variant.IsActive.Should().BeTrue();

        // Verify changes were saved (even if status didn't change)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyInactiveVariant_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = false
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Deactivate the variant first
        variant.Deactivate();

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
        variant.IsActive.Should().BeFalse();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = false
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
        variant.IsActive.Should().BeFalse();

        // Verify changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = true
        };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock save entities to throw exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Variant.UpdateStatusFailed");
        result.Error.Message.Should().Contain("Failed to update variant status");
    }

    [Fact]
    public async Task Handle_VariantGetByIdError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new UpdateVariantStatusCommandV1
        {
            Id = variantId,
            IsActive = true
        };

        // Mock variant retrieval to throw exception
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ThrowsAsync(new Exception("Database timeout"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Variant.UpdateStatusFailed");
        result.Error.Message.Should().Contain("Failed to update variant status");
    }
}
