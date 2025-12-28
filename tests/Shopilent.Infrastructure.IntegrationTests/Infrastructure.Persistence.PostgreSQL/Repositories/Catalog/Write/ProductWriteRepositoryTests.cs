using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;

[Collection("IntegrationTests")]
public class ProductWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;

    public ProductWriteRepositoryTests(IntegrationTestFixture integrationTestFixture) : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _categoryWriteRepository = GetService<ICategoryWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidProduct_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();

        // Act
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductReader.GetByIdAsync(product.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be(product.Name);
        result.Description.Should().Be(product.Description);
        result.Slug.Should().Be(product.Slug.Value);
        result.BasePrice.Should().Be(product.BasePrice.Amount);
        result.Currency.Should().Be(product.BasePrice.Currency);
        result.IsActive.Should().Be(product.IsActive);
    }

    [Fact]
    public async Task AddAsync_ProductWithUniqueSlug_ShouldPersistSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var uniqueSlug = $"unique-product-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random()
            .WithSlug(uniqueSlug)
            .WithName("Unique Product")
            .Build();

        // Act
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductWriter.GetBySlugAsync(uniqueSlug);
        result.Should().NotBeNull();
        result!.Slug.Value.Should().Be(uniqueSlug);
        result.Name.Should().Be("Unique Product");
    }

    [Fact]
    public async Task AddAsync_DuplicateSlug_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var duplicateSlug = $"duplicate-slug-{DateTime.Now.Ticks}";

        var product1 = ProductBuilder.Random()
            .WithSlug(duplicateSlug)
            .WithName("First Product")
            .Build();

        var product2 = ProductBuilder.Random()
            .WithSlug(duplicateSlug)
            .WithName("Second Product")
            .Build();

        await _unitOfWork.ProductWriter.AddAsync(product1);
        await _unitOfWork.SaveChangesAsync();

        // Act & Assert
        await _unitOfWork.ProductWriter.AddAsync(product2);
        var action = () => _unitOfWork.SaveChangesAsync();
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingProduct_ShouldModifyProduct()
    {
        // Arrange
        await ResetDatabaseAsync();
        var originalProduct = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(originalProduct);
        await _unitOfWork.SaveChangesAsync();

        // Detach original entity to simulate real-world scenario
        DbContext.Entry(originalProduct).State = EntityState.Detached;

        // Act - Load fresh entity and update
        var existingProduct = await _unitOfWork.ProductWriter.GetByIdAsync(originalProduct.Id);
        var newName = "Updated Product Name";
        var newDescription = "Updated product description";
        var newPrice = Money.Create(199.99m, "USD").Value;
        var newSlug = Slug.Create($"updated-product-{DateTime.Now.Ticks}").Value;

        existingProduct!.Update(newName, newSlug, newPrice, newDescription);
        await _unitOfWork.ProductWriter.UpdateAsync(existingProduct);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedProduct = await _unitOfWork.ProductReader.GetByIdAsync(originalProduct.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Name.Should().Be(newName);
        updatedProduct.Description.Should().Be(newDescription);
        updatedProduct.BasePrice.Should().Be(199.99m);
        updatedProduct.Slug.Should().Be(newSlug.Value);
    }

    [Fact]
    public async Task UpdateAsync_ChangeProductStatus_ShouldUpdateIsActive()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().AsActive().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(product).State = EntityState.Detached;

        // Act - Load fresh entity and deactivate
        var existingProduct = await _unitOfWork.ProductWriter.GetByIdAsync(product.Id);
        existingProduct!.Deactivate();
        await _unitOfWork.ProductWriter.UpdateAsync(existingProduct);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductReader.GetByIdAsync(product.Id);
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ExistingProduct_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _unitOfWork.ProductWriter.DeleteAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductReader.GetByIdAsync(product.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ShouldReturnProduct()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.ProductWriter.GetByIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be(product.Name);
        result.Slug.Value.Should().Be(product.Slug.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProduct_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.ProductWriter.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_ExistingSlug_ShouldReturnProduct()
    {
        // Arrange
        await ResetDatabaseAsync();
        var uniqueSlug = $"get-by-slug-test-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random()
            .WithSlug(uniqueSlug)
            .Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.ProductWriter.GetBySlugAsync(uniqueSlug);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Slug.Value.Should().Be(uniqueSlug);
    }

    [Fact]
    public async Task GetBySlugAsync_NonExistentSlug_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSlug = "non-existent-slug";

        // Act
        var result = await _unitOfWork.ProductWriter.GetBySlugAsync(nonExistentSlug);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlug_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var slug = $"slug-exists-test-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random().WithSlug(slug).Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.ProductWriter.SlugExistsAsync(slug);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SlugExistsAsync_NonExistentSlug_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSlug = $"non-existent-{DateTime.Now.Ticks}";

        // Act
        var result = await _unitOfWork.ProductWriter.SlugExistsAsync(nonExistentSlug);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlugWithExcludeId_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var slug = $"exclude-test-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random().WithSlug(slug).Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act - Exclude the current product ID
        var result = await _unitOfWork.ProductWriter.SlugExistsAsync(slug, product.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SkuExistsAsync_ExistingSku_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var sku = $"SKU-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random().Build();
        product.Update(product.Name, product.Slug, product.BasePrice, product.Description, sku);
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.ProductWriter.SkuExistsAsync(sku);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SkuExistsAsync_NonExistentSku_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSku = $"NON-EXISTENT-{DateTime.Now.Ticks}";

        // Act
        var result = await _unitOfWork.ProductWriter.SkuExistsAsync(nonExistentSku);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SkuExistsAsync_ExistingSkuWithExcludeId_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var sku = $"EXCLUDE-SKU-{DateTime.Now.Ticks}";
        var product = ProductBuilder.Random().Build();
        product.Update(product.Name, product.Slug, product.BasePrice, product.Description, sku);
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Act - Exclude the current product ID
        var result = await _unitOfWork.ProductWriter.SkuExistsAsync(sku, product.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentUpdate_SameProduct_ShouldHandleOptimisticConcurrency()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();
        var productId = product.Id;

        // Create separate service scopes to simulate true concurrent access
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Get two instances of the same product from separate contexts
        var product1 = await unitOfWork1.ProductWriter.GetByIdAsync(productId);
        var product2 = await unitOfWork2.ProductWriter.GetByIdAsync(productId);

        product1.Should().NotBeNull();
        product2.Should().NotBeNull();

        // Verify both products have the same initial version
        product1!.Version.Should().Be(product2!.Version);

        // Modify both instances
        var slug1 = Slug.Create($"first-update-{DateTime.Now.Ticks}").Value;
        var slug2 = Slug.Create($"second-update-{DateTime.Now.Ticks}").Value;

        product1.Update("First Update", slug1, product1.BasePrice, "First description");
        product2.Update("Second Update", slug2, product2.BasePrice, "Second description");

        // Act & Assert
        // First update should succeed
        await unitOfWork1.ProductWriter.UpdateAsync(product1);
        await unitOfWork1.SaveChangesAsync();

        // Second update should fail due to concurrency conflict
        await unitOfWork2.ProductWriter.UpdateAsync(product2);

        var action = () => unitOfWork2.SaveChangesAsync();
        await action.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task AddProductWithCategory_ValidCategoryAssociation_ShouldPersistCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create category first
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        // Create product and add category
        var product = ProductBuilder.Random().Build();
        product.AddCategory(category);

        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductReader.GetByIdAsync(product.Id);
        result.Should().NotBeNull();

        // Verify category association through product lookup
        var productsInCategory = await _unitOfWork.ProductReader.GetByCategoryAsync(category.Id);
        productsInCategory.Should().Contain(p => p.Id == product.Id);
    }

    [Fact]
    public async Task UpdateProductMetadata_ValidKeyValue_ShouldPersistMetadata()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(product).State = EntityState.Detached;

        // Act - Load fresh entity and update metadata
        var existingProduct = await _unitOfWork.ProductWriter.GetByIdAsync(product.Id);
        existingProduct!.UpdateMetadata("brand", "TestBrand");
        existingProduct.UpdateMetadata("color", "Blue");

        await _unitOfWork.ProductWriter.UpdateAsync(existingProduct);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.ProductWriter.GetByIdAsync(product.Id);
        result.Should().NotBeNull();
        result!.Metadata.Should().ContainKey("brand");
        result.Metadata["brand"].Should().Be("TestBrand");
        result.Metadata.Should().ContainKey("color");
        result.Metadata["color"].Should().Be("Blue");
    }
}
