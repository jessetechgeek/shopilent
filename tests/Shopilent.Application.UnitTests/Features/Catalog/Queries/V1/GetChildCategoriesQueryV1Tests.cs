using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetChildCategories.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetChildCategoriesQueryV1Tests : TestBase
{
    private readonly GetChildCategoriesQueryHandlerV1 _handler;

    public GetChildCategoriesQueryV1Tests()
    {
        _handler = new GetChildCategoriesQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetChildCategoriesQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidParentId_ReturnsChildCategories()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var query = new GetChildCategoriesQueryV1 { ParentId = parentId };

        // Mock parent category exists
        var parentCategory = new CategoryDto
        {
            Id = parentId,
            Name = "Parent Category",
            Slug = "parent-category",
            IsActive = true
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(parentCategory);

        // Create child categories
        var childCategories = new List<CategoryDto>
        {
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Child Category 1",
                Slug = "child-category-1",
                ParentId = parentId,
                Level = 1,
                Path = "/parent-category/child-category-1",
                IsActive = true
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Child Category 2",
                Slug = "child-category-2",
                ParentId = parentId,
                Level = 1,
                Path = "/parent-category/child-category-2",
                IsActive = true
            }
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(parentId, CancellationToken))
            .ReturnsAsync(childCategories);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Should().Contain(c => c.Name == "Child Category 1");
        result.Value.Should().Contain(c => c.Name == "Child Category 2");
    }

    [Fact]
    public async Task Handle_WithNonExistentParentId_ReturnsErrorResult()
    {
        // Arrange
        var nonExistentParentId = Guid.NewGuid();
        var query = new GetChildCategoriesQueryV1 { ParentId = nonExistentParentId };

        // Mock parent category does not exist
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(nonExistentParentId, CancellationToken))
            .ReturnsAsync((CategoryDto)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(CategoryErrors.NotFound(nonExistentParentId).Code);
    }

    [Fact]
    public async Task Handle_WithNoChildCategories_ReturnsEmptyList()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var query = new GetChildCategoriesQueryV1 { ParentId = parentId };

        // Mock parent category exists
        var parentCategory = new CategoryDto
        {
            Id = parentId,
            Name = "Parent Category",
            Slug = "parent-category",
            IsActive = true
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(parentCategory);

        // Mock empty child categories list
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(parentId, CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var query = new GetChildCategoriesQueryV1 { ParentId = parentId };

        // Mock parent category exists
        var parentCategory = new CategoryDto
        {
            Id = parentId,
            Name = "Parent Category",
            Slug = "parent-category",
            IsActive = true
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetByIdAsync(parentId, CancellationToken))
            .ReturnsAsync(parentCategory);

        // Mock exception
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetChildCategoriesAsync(parentId, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Categories.GetChildCategoriesFailed");
        result.Error.Message.Should().Contain("Test exception");
    }
}
