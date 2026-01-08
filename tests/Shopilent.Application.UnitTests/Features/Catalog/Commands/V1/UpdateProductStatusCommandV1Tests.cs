using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateProductStatus.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateProductStatusCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create a product with a specific ID for testing
    /// </summary>
    private static Product CreateProductWithId(Guid id, string name, string slug, decimal price, bool isActive = true)
    {
        var slugValue = Slug.Create(slug).Value;
        var moneyValue = Money.Create(price, "USD").Value;
        var product = isActive
            ? Product.Create(name, slugValue, moneyValue).Value
            : Product.CreateInactive(name, slugValue, moneyValue).Value;
        var idProperty = typeof(Product).GetProperty("Id");
        idProperty?.SetValue(product, id);
        return product;
    }

    public UpdateProductStatusCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateProductStatusCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateProductStatusCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateProductStatusCommandV1>, UpdateProductStatusCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ActivateInactiveProduct_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = true
        };

        // Create existing inactive product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, false);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product was activated
        existingProduct.IsActive.Should().BeTrue();

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivateActiveProduct_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = false
        };

        // Create existing active product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, true);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product was deactivated
        existingProduct.IsActive.Should().BeFalse();

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ActivateAlreadyActiveProduct_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = true
        };

        // Create existing active product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, true);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product remains active
        existingProduct.IsActive.Should().BeTrue();

        // Verify the changes were saved (even though no change was made, the handler still saves)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivateAlreadyInactiveProduct_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = false
        };

        // Create existing inactive product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, false);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product remains inactive
        existingProduct.IsActive.Should().BeFalse();

        // Verify the changes were saved (even though no change was made, the handler still saves)
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = nonExistentId,
            IsActive = true
        };

        // Mock that product doesn't exist
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentId, CancellationToken))
            .ReturnsAsync((Product)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ProductErrors.NotFound(nonExistentId).Code);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_UpdatesStatusWithoutAuditInfo()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = false
        };

        // Create existing active product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, true);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the product status was still updated successfully
        existingProduct.IsActive.Should().BeFalse();

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExceptionDuringProcessing_ReturnsFailure()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new UpdateProductStatusCommandV1
        {
            Id = productId,
            IsActive = true
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m, false);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock SaveChangesAsync to throw an exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Product.UpdateStatusFailed");
        result.Error.Message.Should().Contain("Failed to update product status");

        // Verify SaveChangesAsync was attempted
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
