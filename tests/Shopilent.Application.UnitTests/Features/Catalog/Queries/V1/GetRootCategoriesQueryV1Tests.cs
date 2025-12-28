using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetRootCategories.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetRootCategoriesQueryV1Tests : TestBase
{
    private readonly GetRootCategoriesQueryHandlerV1 _handler;

    public GetRootCategoriesQueryV1Tests()
    {
        _handler = new GetRootCategoriesQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetRootCategoriesQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithExistingRootCategories_ReturnsCategories()
    {
        // Arrange
        var query = new GetRootCategoriesQueryV1();

        // Create root categories
        var rootCategories = new List<CategoryDto>
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
                IsActive = true
            }
        };

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetRootCategoriesAsync(CancellationToken))
            .ReturnsAsync(rootCategories);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Should().Contain(c => c.Name == "Root Category 1");
        result.Value.Should().Contain(c => c.Name == "Root Category 2");

        // Verify all categories are actually root categories (level 0, null parent)
        result.Value.Should().OnlyContain(c => c.Level == 0 && c.ParentId == null);
    }

    [Fact]
    public async Task Handle_WithNoRootCategories_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetRootCategoriesQueryV1();

        // Mock empty root categories list
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetRootCategoriesAsync(CancellationToken))
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
        var query = new GetRootCategoriesQueryV1();

        // Mock exception
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetRootCategoriesAsync(CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Categories.GetRootCategoriesFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var query = new GetRootCategoriesQueryV1();

        // Mock successful repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetRootCategoriesAsync(CancellationToken))
            .ReturnsAsync(new List<CategoryDto>());

        // Act - no need to actually check the result here
        await _handler.Handle(query, CancellationToken);

        // Assert that cache settings are properly configured
        query.CacheKey.Should().Be("root-categories");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }
}
