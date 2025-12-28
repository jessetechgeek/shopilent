using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetPaginatedCategories.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Models;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetPaginatedCategoriesQueryV1Tests : TestBase
{
    private readonly GetPaginatedCategoriesQueryHandlerV1 _handler;

    public GetPaginatedCategoriesQueryV1Tests()
    {
        _handler = new GetPaginatedCategoriesQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetPaginatedCategoriesQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithDefaultParameters_ReturnsPaginatedCategories()
    {
        // Arrange
        var query = new GetPaginatedCategoriesQueryV1
        {
            // Default values:
            // PageNumber = 1
            // PageSize = 10
            // SortColumn = "Name"
            // SortDescending = false
        };

        // Create a paginated result with some categories
        var categories = new List<CategoryDto>
        {
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Category A",
                Slug = "category-a",
                Level = 0
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Category B",
                Slug = "category-b",
                Level = 0
            }
        };

        var paginatedResult = new PaginatedResult<CategoryDto>(
            items: categories,
            count: 2,
            pageNumber: 1,
            pageSize: 10
        );

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetPaginatedAsync(
                1, 10, "Name", false, CancellationToken))
            .ReturnsAsync(paginatedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(2);
        result.Value.TotalPages.Should().Be(1);
        result.Value.Items.Count.Should().Be(2);
        result.Value.Items.Should().Contain(c => c.Name == "Category A");
        result.Value.Items.Should().Contain(c => c.Name == "Category B");
    }

    [Fact]
    public async Task Handle_WithCustomParameters_ReturnsPaginatedCategories()
    {
        // Arrange
        var query = new GetPaginatedCategoriesQueryV1
        {
            PageNumber = 2,
            PageSize = 5,
            SortColumn = "Slug",
            SortDescending = true
        };

        // Create a paginated result with some categories
        var categories = new List<CategoryDto>
        {
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Category C",
                Slug = "category-c",
                Level = 0
            },
            new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = "Category D",
                Slug = "category-d",
                Level = 0
            }
        };

        var paginatedResult = new PaginatedResult<CategoryDto>(
            items: categories,
            count: 10,
            pageNumber: 2,
            pageSize: 5
        );

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetPaginatedAsync(
                2, 5, "Slug", true, CancellationToken))
            .ReturnsAsync(paginatedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageNumber.Should().Be(2);
        result.Value.PageSize.Should().Be(5);
        result.Value.TotalCount.Should().Be(10);
        result.Value.TotalPages.Should().Be(2);
        result.Value.Items.Count.Should().Be(2);

        // Verify parameters were passed correctly
        Fixture.MockCategoryReadRepository.Verify(
            repo => repo.GetPaginatedAsync(
                2, 5, "Slug", true, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyPage_ReturnsEmptyItemsList()
    {
        // Arrange
        var query = new GetPaginatedCategoriesQueryV1
        {
            PageNumber = 3, // Page beyond available data
            PageSize = 10
        };

        // Create an empty paginated result
        var paginatedResult = new PaginatedResult<CategoryDto>(
            items: new List<CategoryDto>(), // Empty items
            count: 15, // 15 total items, but none on page 3 with pageSize 10
            pageNumber: 3,
            pageSize: 10
        );

        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetPaginatedAsync(
                3, 10, "Name", false, CancellationToken))
            .ReturnsAsync(paginatedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageNumber.Should().Be(3);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(15);
        result.Value.TotalPages.Should().Be(2);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetPaginatedCategoriesQueryV1();

        // Mock exception
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetPaginatedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Categories.GetPaginatedFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCacheKeyAndExpirationAreSet()
    {
        // Arrange
        var query = new GetPaginatedCategoriesQueryV1
        {
            PageNumber = 2,
            PageSize = 15,
            SortColumn = "CreatedAt",
            SortDescending = true
        };

        // Create paginated result
        var paginatedResult = new PaginatedResult<CategoryDto>(
            items: new List<CategoryDto>(),
            count: 30,
            pageNumber: 2,
            pageSize: 15
        );

        // Mock successful repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetPaginatedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), CancellationToken))
            .ReturnsAsync(paginatedResult);

        // Act - no need to actually check the result here
        await _handler.Handle(query, CancellationToken);

        // Assert that cache settings are properly configured with query parameters
        query.CacheKey.Should().Be($"categories-page-{query.PageNumber}-size-{query.PageSize}-sort-{query.SortColumn}-{query.SortDescending}");
        query.Expiration.Should().NotBeNull();
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }
}
