using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Catalog.Commands.UpdateCategoryParent.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Commands.V1;

public class UpdateCategoryParentCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateCategoryParentCommandV1Tests()
    {
        // Set up MediatR pipeline
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockCategoryWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCategoryReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateCategoryParentCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateCategoryParentCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateCategoryParentCommandV1>, UpdateCategoryParentCommandValidatorV1>();

        // Get the mediator
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task UpdateCategoryParent_WithValidParent_ReturnsSuccessfulResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Child Category")
            .WithSlug("child-category")
            .Build();

        var parentCategory = new CategoryBuilder()
            .WithId(parentId)
            .WithName("Parent Category")
            .WithSlug("parent-category")
            .Build();

        var command = new UpdateCategoryParentCommandV1
        {
            Id = categoryId,
            ParentId = parentId
        };

        // Mock category retrievals
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(parentCategory);

        // Mock parent category reader for response
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(new Domain.Catalog.DTOs.CategoryDto
            {
                Id = parentId,
                Name = "Parent Category"
            });

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(categoryId);
        result.Value.ParentId.Should().Be(parentId);
        result.Value.ParentName.Should().Be("Parent Category");
        result.Value.Level.Should().Be(1); // Level 1 as child of parent

        // Verify the category was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCategoryParent_RemovingParent_SetsCategoryAsRoot()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var originalParentId = Guid.NewGuid();

        // Create a category with a parent
        var parentCategory = new CategoryBuilder()
            .WithId(originalParentId)
            .WithName("Original Parent")
            .WithSlug("original-parent")
            .Build();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Child Category")
            .WithSlug("child-category")
            .WithParent(parentCategory)
            .Build();

        var command = new UpdateCategoryParentCommandV1
        {
            Id = categoryId,
            ParentId = null // Remove parent
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Setup authenticated user for audit info
        var userId = Guid.NewGuid();
        Fixture.SetAuthenticatedUser(userId);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(categoryId);
        result.Value.ParentId.Should().BeNull();
        result.Value.ParentName.Should().BeNull();
        result.Value.Level.Should().Be(0); // Level 0 as root category

        // Verify the category was saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCategoryParent_WithNonExistentCategory_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var command = new UpdateCategoryParentCommandV1
        {
            Id = categoryId,
            ParentId = parentId
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

        // Verify the category was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCategoryParent_WithNonExistentParent_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var nonExistentParentId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Child Category")
            .WithSlug("child-category")
            .Build();

        var command = new UpdateCategoryParentCommandV1
        {
            Id = categoryId,
            ParentId = nonExistentParentId
        };

        // Mock category retrieval but not parent
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentParentId, CancellationToken))
            .ReturnsAsync((Category)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.NotFound(nonExistentParentId).Code);

        // Verify the category was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCategoryParent_SettingCategoryAsItsOwnParent_ReturnsErrorResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        var category = new CategoryBuilder()
            .WithId(categoryId)
            .WithName("Self Parent Category")
            .WithSlug("self-parent-category")
            .Build();

        var command = new UpdateCategoryParentCommandV1
        {
            Id = categoryId,
            ParentId = categoryId // Same as category ID
        };

        // Mock category retrieval
        Fixture.MockCategoryWriteRepository
            .Setup(repo => repo.GetByIdAsync(categoryId, CancellationToken))
            .ReturnsAsync(category);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.CircularReference.Code);

        // Verify the category was not saved
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }
}
