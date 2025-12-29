using Microsoft.EntityFrameworkCore;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;

[Collection("IntegrationTests")]
public class CategoryWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;
    private ICategoryReadRepository _categoryReadRepository = null!;


    public CategoryWriteRepositoryTests(IntegrationTestFixture integrationTestFixture) : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _categoryWriteRepository = GetService<ICategoryWriteRepository>();
        _categoryReadRepository = GetService<ICategoryReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidCategory_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().Build();

        // Act
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Assert
        var persisted = await _categoryReadRepository.GetByIdAsync(category.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(category.Name);
        persisted.Description.Should().Be(category.Description);
        persisted.Slug.Should().Be(category.Slug.Value);
    }

    [Fact]
    public async Task AddAsync_DuplicateSlug_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var existingCategory = CategoryBuilder.Random().WithSlug("duplicate-slug").Build();
        await _categoryWriteRepository.AddAsync(existingCategory);
        await _unitOfWork.CommitAsync();

        var duplicateCategory = CategoryBuilder.Random().WithSlug("duplicate-slug").Build();

        // Act & Assert
        await _categoryWriteRepository.AddAsync(duplicateCategory);

        // The constraint violation should occur when SaveChangesAsync is called
        var action = () => _unitOfWork.CommitAsync();
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingCategory_ShouldModifyEntity()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Detach to simulate a fresh load
        DbContext.Entry(category).State = EntityState.Detached;

        var updatedCategory = await _categoryWriteRepository.GetByIdAsync(category.Id);
        var updateSlug = Slug.Create("updated-name").Value;
        updatedCategory!.Update("Updated Name", updateSlug, "Updated Description");

        // Act
        await _categoryWriteRepository.UpdateAsync(updatedCategory);
        await _unitOfWork.CommitAsync();

        // Assert
        var persisted = await _categoryReadRepository.GetByIdAsync(category.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Updated Name");
        persisted.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task DeleteAsync_ExistingCategory_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        await _categoryWriteRepository.DeleteAsync(category);
        await _unitOfWork.CommitAsync();

        // Assert
        var persisted = await _categoryReadRepository.GetByIdAsync(category.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCategory_ShouldReturnCategory()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _categoryWriteRepository.GetByIdAsync(category.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(category.Id);
        result.Name.Should().Be(category.Name);
        result.Slug.Value.Should().Be(category.Slug.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentCategory_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _categoryWriteRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_ExistingCategory_ShouldReturnCategory()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().WithSlug("test-category").Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _categoryWriteRepository.GetBySlugAsync("test-category");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(category.Id);
        result.Slug.Value.Should().Be("test-category");
    }

    [Fact]
    public async Task GetBySlugAsync_NonExistentSlug_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _categoryWriteRepository.GetBySlugAsync("non-existent-slug");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlug_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().WithSlug("existing-slug").Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        var exists = await _categoryWriteRepository.SlugExistsAsync("existing-slug");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SlugExistsAsync_NonExistentSlug_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var exists = await _categoryWriteRepository.SlugExistsAsync("non-existent-slug");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SlugExistsAsync_WithExcludeId_ShouldExcludeSpecifiedCategory()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().WithSlug("test-slug").Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        var exists = await _categoryWriteRepository.SlugExistsAsync("test-slug", category.Id);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_CategoryWithParent_ShouldSetParentRelationship()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create parent category first
        var parentCategory = CategoryBuilder.Random().WithName("Electronics").Build();
        await _categoryWriteRepository.AddAsync(parentCategory);
        await _unitOfWork.CommitAsync();

        // Create child category with proper parent relationship
        var childCategory = CategoryBuilder.Random()
            .WithName("Computers")
            .WithParentCategory(parentCategory)
            .Build();

        // Act
        await _categoryWriteRepository.AddAsync(childCategory);
        await _unitOfWork.CommitAsync();

        // Assert
        childCategory.Should().NotBeNull();
        childCategory.ParentId.Should().Be(parentCategory.Id);
        childCategory.Level.Should().Be(1); // Parent is level 0, child should be level 1
        childCategory.Path.Should().Contain(parentCategory.Slug.Value);
        childCategory.Path.Should().Contain(childCategory.Slug.Value);

        // Verify persisted in database
        var persisted = await _categoryReadRepository.GetByIdAsync(childCategory.Id);
        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(childCategory.Id);
        persisted.Name.Should().Be("Computers");
    }

    [Fact]
    public async Task AddAsync_MultiLevelHierarchy_ShouldCalculateCorrectLevelsAndPaths()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create 3-level hierarchy: Electronics -> Computers -> Laptops
        var electronics = CategoryBuilder.Random().WithName("Electronics").Build();
        await _categoryWriteRepository.AddAsync(electronics);
        await _unitOfWork.CommitAsync();

        var computers = CategoryBuilder.Random()
            .WithName("Computers")
            .WithParentCategory(electronics)
            .Build();
        await _categoryWriteRepository.AddAsync(computers);
        await _unitOfWork.CommitAsync();

        var laptops = CategoryBuilder.Random()
            .WithName("Laptops")
            .WithParentCategory(computers)
            .Build();

        // Act
        await _categoryWriteRepository.AddAsync(laptops);
        await _unitOfWork.CommitAsync();

        // Assert
        laptops.Should().NotBeNull();

        // Check levels
        electronics.Level.Should().Be(0);
        computers.Level.Should().Be(1);
        laptops.Level.Should().Be(2);

        // Check paths
        electronics.Path.Should().Be($"/{electronics.Slug.Value}");
        computers.Path.Should().Be($"/{electronics.Slug.Value}/{computers.Slug.Value}");
        laptops.Path.Should().Be($"/{electronics.Slug.Value}/{computers.Slug.Value}/{laptops.Slug.Value}");

        // Verify parent relationships
        computers.ParentId.Should().Be(electronics.Id);
        laptops.ParentId.Should().Be(computers.Id);
    }

    [Fact]
    public async Task AddAsync_RootCategory_ShouldHaveZeroLevelAndCorrectPath()
    {
        // Arrange
        await ResetDatabaseAsync();
        var rootCategory = CategoryBuilder.Random().WithName("Root Category").WithoutParent().Build();

        // Act
        await _categoryWriteRepository.AddAsync(rootCategory);
        await _unitOfWork.CommitAsync();

        // Assert
        rootCategory.Should().NotBeNull();
        rootCategory.ParentId.HasValue.Should().BeFalse();
        rootCategory.Level.Should().Be(0);
        rootCategory.Path.Should().Be($"/{rootCategory.Slug.Value}");
    }
}
