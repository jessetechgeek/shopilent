using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.DeleteCategory.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class DeleteCategoryCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public DeleteCategoryCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCategoryWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCategoryReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeleteCategoryCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<DeleteCategoryCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<DeleteCategoryCommandV1>, DeleteCategoryCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task DeleteCategory_WithValidCategory_ReturnsSuccessfulResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Test Category")
            .WithSlug("test-category")
            .Build();

        var command = new DeleteCategoryCommandV1
        {
            Id = categoryId
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Mock that category has no children
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(categoryId, CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Mock that category has no products
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByCategoryAsync(categoryId, CancellationToken))
            .ReturnsAsync(new List<ProductDto>());

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify category was deleted
        Fixture.MockCategoryWriteRepository.Verify(
            repo => repo.DeleteAsync(category, CancellationToken),
            Times.Once);

        // Verify the changes were saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DeleteCategory_WithNonExistentCategory_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var command = new DeleteCategoryCommandV1
        {
            Id = categoryId
        };

        // Mock that category doesn't exist
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync((Category)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.NotFound(categoryId).Code);

        // Verify category was not deleted
        Fixture.MockCategoryWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Category>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteCategory_WithChildCategories_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Parent Category")
            .WithSlug("parent-category")
            .Build();

        var command = new DeleteCategoryCommandV1
        {
            Id = categoryId
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Mock that category has child categories
        var childCategories = new List<CategoryDto>
        {
            new CategoryDto { Id = Guid.NewGuid(), Name = "Child Category 1" },
            new CategoryDto { Id = Guid.NewGuid(), Name = "Child Category 2" }
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(categoryId, CancellationToken))
            .ReturnsAsync(childCategories);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.CannotDeleteWithChildren.Code);

        // Verify category was not deleted
        Fixture.MockCategoryWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Category>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteCategory_WithAssociatedProducts_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Test Category")
            .WithSlug("test-category")
            .Build();

        var command = new DeleteCategoryCommandV1
        {
            Id = categoryId
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Mock that category has no children
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(categoryId, CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Mock that category has associated products
        var products = new List<ProductDto>
        {
            new ProductDto { Id = Guid.NewGuid(), Name = "Product 1" },
            new ProductDto { Id = Guid.NewGuid(), Name = "Product 2" }
        };

        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByCategoryAsync(categoryId, CancellationToken))
            .ReturnsAsync(products);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.CannotDeleteWithProducts.Code);

        // Verify category was not deleted
        Fixture.MockCategoryWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<Category>(), CancellationToken),
            Times.Never);

        // Verify the changes were not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeleteCategory_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Test Category")
            .WithSlug("test-category")
            .Build();

        var command = new DeleteCategoryCommandV1
        {
            Id = categoryId
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Mock that category has no children
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(categoryId, CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Mock that category has no products
        Fixture.MockProductReadRepository
            .Setup(repo => repo.GetByCategoryAsync(categoryId, CancellationToken))
            .ReturnsAsync(new List<ProductDto>());

        // Make DeleteAsync throw an exception
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.DeleteAsync(category, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Category.DeleteFailed");
        result.Error.Message.Should().Contain("Failed to delete category");
    }
}
