using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetCategoriesDatatable.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Models;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetCategoriesDatatableQueryV1Tests : TestBase
{
    private readonly GetCategoriesDatatableQueryHandlerV1 _handler;

    public GetCategoriesDatatableQueryV1Tests()
    {
        _handler = new GetCategoriesDatatableQueryHandlerV1(
            Fixture.MockCategoryReadRepository.Object,
            Fixture.GetLogger<GetCategoriesDatatableQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsFormattedDatatableResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "test" },
            Order = new List<DataTableOrder>
            {
                new DataTableOrder { Column = 0, Dir = "asc" }
            },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "Name", Searchable = true, Orderable = true }
            }
        };

        var query = new GetCategoriesDatatableQueryV1
        {
            Request = request
        };

        // Create categories with parent relationships
        var parentId1 = Guid.NewGuid();
        var parentId2 = Guid.NewGuid();

        // Use CategoryDetailDto instead of CategoryDto
        var categories = new List<CategoryDetailDto>
        {
            new CategoryDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Category 1",
                Slug = "test-category-1",
                Description = "Test description 1",
                ParentId = parentId1,
                ParentName = "Parent Category 1", // Set directly in the DTO
                Level = 1,
                IsActive = true,
                ProductCount = 2, // Set directly in the DTO
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new CategoryDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Test Category 2",
                Slug = "test-category-2",
                Description = "Test description 2",
                ParentId = parentId2,
                ParentName = "Parent Category 2", // Set directly in the DTO
                Level = 1,
                IsActive = false,
                ProductCount = 1, // Set directly in the DTO
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        // Setup the datatable result with CategoryDetailDto
        var dataTableResult = new DataTableResult<CategoryDetailDto>(
            draw: 1,
            recordsTotal: 25,
            recordsFiltered: 2,
            data: categories
        );

        // Mock the repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetCategoryDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(dataTableResult);

        // No need to mock parent category retrieval since we're using CategoryDetailDto with ParentName already set

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify datatable metadata
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(25);
        result.Value.RecordsFiltered.Should().Be(2);
        result.Value.Data.Count.Should().Be(2);

        // Verify the CategoryDatatableDto properties
        var firstCategory = result.Value.Data.First(c => c.Name == "Test Category 1");
        firstCategory.Id.Should().Be(categories[0].Id);
        firstCategory.Slug.Should().Be("test-category-1");
        firstCategory.Description.Should().Be("Test description 1");
        firstCategory.ParentId.Should().Be(parentId1);
        firstCategory.ParentName.Should().Be("Parent Category 1");
        firstCategory.Level.Should().Be(1);
        firstCategory.IsActive.Should().BeTrue();
        firstCategory.ProductCount.Should().Be(2);

        var secondCategory = result.Value.Data.First(c => c.Name == "Test Category 2");
        secondCategory.Id.Should().Be(categories[1].Id);
        secondCategory.Slug.Should().Be("test-category-2");
        secondCategory.Description.Should().Be("Test description 2");
        secondCategory.ParentId.Should().Be(parentId2);
        secondCategory.ParentName.Should().Be("Parent Category 2");
        secondCategory.Level.Should().Be(1);
        secondCategory.IsActive.Should().BeFalse();
        secondCategory.ProductCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithMissingParentCategories_HandlesGracefully()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "" },
            Order = new List<DataTableOrder>
            {
                new DataTableOrder { Column = 0, Dir = "asc" }
            },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "Name", Searchable = true, Orderable = true }
            }
        };

        var query = new GetCategoriesDatatableQueryV1
        {
            Request = request
        };

        // Create category with non-existent parent
        var nonExistentParentId = Guid.NewGuid();
        var categories = new List<CategoryDetailDto>
        {
            new CategoryDetailDto
            {
                Id = Guid.NewGuid(),
                Name = "Orphan Category",
                Slug = "orphan-category",
                ParentId = nonExistentParentId,
                ParentName = null, // No parent name
                Level = 1,
                IsActive = true,
                ProductCount = 0 // No products
            }
        };

        var dataTableResult = new DataTableResult<CategoryDetailDto>(
            draw: 1,
            recordsTotal: 1,
            recordsFiltered: 1,
            data: categories
        );

        // Mock the repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetCategoryDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(dataTableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Count.Should().Be(1);

        var categoryDto = result.Value.Data.First();
        categoryDto.Name.Should().Be("Orphan Category");
        categoryDto.ParentId.Should().Be(nonExistentParentId);
        categoryDto.ParentName.Should().BeNull(); // Parent name should be null
        categoryDto.ProductCount.Should().Be(0); // No products
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetCategoriesDatatableQueryV1
        {
            Request = request
        };

        // Mock exception
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetCategoryDetailDataTableAsync(request, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Categories.GetDataTableFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_WithNullDataTableRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var query = new GetCategoriesDatatableQueryV1
        {
            Request = null
        };

        // Act & Assert
        await FluentActions.Invoking(async () =>
            await _handler.Handle(query, CancellationToken))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithZeroDraw_ReturnsCorrectDrawNumber()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 0, // Zero draw number should be preserved
            Start = 0,
            Length = 10
        };

        var query = new GetCategoriesDatatableQueryV1
        {
            Request = request
        };

        var dataTableResult = new DataTableResult<CategoryDetailDto>(
            draw: 0,
            recordsTotal: 0,
            recordsFiltered: 0,
            data: new List<CategoryDetailDto>()
        );

        // Mock the repository call
        Fixture.MockCategoryReadRepository
            .Setup(repo => repo.GetCategoryDetailDataTableAsync(request, CancellationToken))
            .ReturnsAsync(dataTableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(0); // Draw number should be preserved
    }
}
