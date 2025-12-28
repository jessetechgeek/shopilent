using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.DeleteProductVariant.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class DeleteProductVariantCommandV1Tests : TestBase
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

    public DeleteProductVariantCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductVariantWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeleteProductVariantCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<DeleteProductVariantCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<DeleteProductVariantCommandV1>, DeleteProductVariantCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidVariantId_ReturnsSuccessResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new DeleteProductVariantCommandV1 { Id = variantId };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock delete operation
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.DeleteAsync(variant, CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the variant was deleted
        Fixture.MockProductVariantWriteRepository.Verify(
            repo => repo.DeleteAsync(variant, CancellationToken),
            Times.Once);

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
        var command = new DeleteProductVariantCommandV1 { Id = nonExistentVariantId };

        // Mock that variant doesn't exist
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentVariantId, CancellationToken))
            .ReturnsAsync((ProductVariant)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.NotFound");
        result.Error.Message.Should().Contain($"Product variant with ID {nonExistentVariantId} was not found");

        // Verify delete was not called
        Fixture.MockProductVariantWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<ProductVariant>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DatabaseError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new DeleteProductVariantCommandV1 { Id = variantId };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock delete operation to throw exception
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.DeleteAsync(variant, CancellationToken))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.DeleteFailed");
        result.Error.Message.Should().Be("Failed to delete product variant");
    }

    [Fact]
    public async Task Handle_SaveEntitiesError_ReturnsFailureResult()
    {
        // Arrange
        var variantId = Guid.NewGuid();
        var command = new DeleteProductVariantCommandV1 { Id = variantId };

        var productId = Guid.NewGuid();
        var variant = CreateProductVariantWithId(variantId, productId, "TEST-VAR-001", 99.99m, 10);

        // Mock variant retrieval
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.GetByIdAsync(variantId, CancellationToken))
            .ReturnsAsync(variant);

        // Mock delete operation
        Fixture.MockProductVariantWriteRepository
            .Setup(repo => repo.DeleteAsync(variant, CancellationToken))
            .Returns(Task.CompletedTask);

        // Mock save entities to throw exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.SaveChangesAsync(CancellationToken))
            .ThrowsAsync(new Exception("Failed to save changes"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ProductVariant.DeleteFailed");
        result.Error.Message.Should().Be("Failed to delete product variant");
    }
}
