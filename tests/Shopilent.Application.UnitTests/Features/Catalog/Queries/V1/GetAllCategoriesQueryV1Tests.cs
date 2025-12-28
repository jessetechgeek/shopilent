using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetAllCategories.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetAllCategoriesQueryV1Tests : TestBase
{
    private readonly GetAllCategoriesQueryHandlerV1 _handler;

    public GetAllCategoriesQueryV1Tests()
    {
        _handler = new GetAllCategoriesQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetAllCategoriesQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithExistingCategories_ReturnsAllCategories()
    {
        // Arrange
        var query = new GetAllCategoriesQueryV1();

        // Create categories of mixed types (root, child, active/inactive)
        var categories = new List<CategoryDto>
        {
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Root Category 1",
                Slug = "root-category-1",
                ParentId = null,
                Level = 0,
                Path = "/root-category-1",
                IsActive = true
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Root Category 2",
                Slug = "root-category-2",
                ParentId = null,
                Level = 0,
                Path = "/root-category-2",
                IsActive = false
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Child Category 1",
                Slug = "child-category-1",
                ParentId = Guid.NewGuid(),
                Level = 1,
                Path = "/parent-category/child-category-1",
                IsActive = true
            }
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(categories);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);
        result.Value.Should().Contain(c => c.Name == "Root Category 1");
        result.Value.Should().Contain(c => c.Name == "Root Category 2");
        result.Value.Should().Contain(c => c.Name == "Child Category 1");
    }

    [Fact]
    public async Task Handle_WithNoCategories_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetAllCategoriesQueryV1();

        // Mock empty categories list
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
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
        var query = new GetAllCategoriesQueryV1();

        // Mock exception
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Categories.GetAllFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var query = new GetAllCategoriesQueryV1();

        // Mock successful repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Act - no need to actually check the result here
        await _handler.Handle(query, CancellationToken);

        // Assert that cache settings are properly configured
        query.CacheKey.Should().Be("all-categories");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Handle_VerifiesFilteringIsNotApplied()
    {
        // Arrange
        var query = new GetAllCategoriesQueryV1();

        // Create mixed categories
        var allCategories = new List<CategoryDto>
        {
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Active Category",
                IsActive = true
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Category",
                IsActive = false
            }
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.ListAllAsync(CancellationToken))
            .ReturnsAsync(allCategories);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert - ensure both active and inactive categories are returned
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Should().Contain(c => c.Name == "Active Category");
        result.Value.Should().Contain(c => c.Name == "Inactive Category");

        // Verify we're using ListAllAsync and not filtering by activity status
        Fixture.MockCategoryReadRepository.Verify(
            repo => repo.ListAllAsync(CancellationToken),
            Times.Once);
    }
}
