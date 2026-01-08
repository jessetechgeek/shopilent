using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.DeleteProduct.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class DeleteProductCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Helper method to create a product with a specific ID for testing
    /// </summary>
    private static Product CreateProductWithId(Guid id, string name, string slug, decimal price)
    {
        var slugValue = Slug.Create(slug).Value;
        var moneyValue = Money.Create(price, "USD").Value;
        var product = Product.Create(name, slugValue, moneyValue).Value;
        var idProperty = typeof(Product).GetProperty("Id");
        idProperty?.SetValue(product, id);
        return product;
    }

    public DeleteProductCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockProductWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeleteProductCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<DeleteProductCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<DeleteProductCommandV1>, DeleteProductCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = productId
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock delete operation
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.DeleteAsync(existingProduct, CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(existingProduct, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = nonExistentId
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

        // Verify delete was never called
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Product>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DeleteOperationThrowsException_ReturnsFailure()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = productId
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock delete operation throws exception
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.DeleteAsync(existingProduct, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Product.DeleteFailed");
        result.Error.Message.Should().Be("Failed to delete product");

        // Verify delete was attempted
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(existingProduct, CancellationToken),
            Times.Once);

        // Verify the changes were not saved due to exception
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SaveOperationThrowsException_ReturnsFailure()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = productId
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock delete operation succeeds
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.DeleteAsync(existingProduct, CancellationToken))
            .Returns(Task.CompletedTask);

        // Mock save operation throws exception
        Fixture.MockUnitOfWork
            .Setup(uow => uow.CommitAsync(CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection error"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Product.DeleteFailed");
        result.Error.Message.Should().Be("Failed to delete product");

        // Verify delete was called
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(existingProduct, CancellationToken),
            Times.Once);

        // Verify save was attempted
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_DeletesSuccessfully()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = productId
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock delete operation
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.DeleteAsync(existingProduct, CancellationToken))
            .Returns(Task.CompletedTask);

        // Setup authenticated user
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(existingProduct, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_DeletesSuccessfully()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new DeleteProductCommandV1
        {
            Id = productId
        };

        // Create existing product
        var existingProduct = CreateProductWithId(productId, "Test Product", "test-product", 29.99m);

        // Mock product retrieval
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.GetByIdAsync(productId, CancellationToken))
            .ReturnsAsync(existingProduct);

        // Mock delete operation
        Fixture.MockProductWriteRepository
            .Setup(repo => repo.DeleteAsync(existingProduct, CancellationToken))
            .Returns(Task.CompletedTask);

        // Setup no authenticated user (uses default unauthenticated state)
        // No setup needed as TestFixture defaults to unauthenticated state

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify delete was called
        Fixture.MockProductWriteRepository.Verify(
            repo => repo.DeleteAsync(existingProduct, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
