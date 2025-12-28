using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;

[Collection("IntegrationTests")]
public class ProductVariantWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IProductVariantWriteRepository _productVariantWriteRepository = null!;
    private IProductVariantReadRepository _productVariantReadRepository = null!;

    public ProductVariantWriteRepositoryTests(IntegrationTestFixture integrationTestFixture) : base(
        integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _productVariantWriteRepository = GetService<IProductVariantWriteRepository>();
        _productVariantReadRepository = GetService<IProductVariantReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidProductVariant_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().BuildForProduct(product);

        // Act
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(variant.Id);
        result.ProductId.Should().Be(product.Id);
        result.Sku.Should().Be(variant.Sku);
        result.StockQuantity.Should().Be(variant.StockQuantity);
        result.IsActive.Should().Be(variant.IsActive);
        if (variant.Price != null)
        {
            result.Price.Should().Be(variant.Price.Amount);
            result.Currency.Should().Be(variant.Price.Currency);
        }
    }

    [Fact]
    public async Task AddAsync_ProductVariantWithUniqueSku_ShouldPersistSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var uniqueSku = $"UNIQUE-VARIANT-{DateTime.Now.Ticks}";
        var variant = ProductVariantBuilder.Random()
            .WithSku(uniqueSku)
            .BuildForProduct(product);

        // Act
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantWriteRepository.GetBySkuAsync(uniqueSku);
        result.Should().NotBeNull();
        result!.Sku.Should().Be(uniqueSku);
        result.Id.Should().Be(variant.Id);
    }

    [Fact]
    public async Task AddAsync_DuplicateSku_ShouldThrowException()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var duplicateSku = $"DUPLICATE-SKU-{DateTime.Now.Ticks}";

        var variant1 = ProductVariantBuilder.Random()
            .WithSku(duplicateSku)
            .BuildForProduct(product);

        var variant2 = ProductVariantBuilder.Random()
            .WithSku(duplicateSku)
            .BuildForProduct(product);

        await _productVariantWriteRepository.AddAsync(variant1);
        await _unitOfWork.SaveChangesAsync();

        // Act & Assert
        await _productVariantWriteRepository.AddAsync(variant2);
        var action = () => _unitOfWork.SaveChangesAsync();
        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingProductVariant_ShouldModifyVariant()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var originalVariant = ProductVariantBuilder.Random().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(originalVariant);
        await _unitOfWork.SaveChangesAsync();

        // Detach original entity to simulate real-world scenario
        DbContext.Entry(originalVariant).State = EntityState.Detached;

        // Act - Load fresh entity and update
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(originalVariant.Id);
        var newSku = $"UPDATED-SKU-{DateTime.Now.Ticks}";
        var newPrice = Money.Create(299.99m, "USD").Value;

        existingVariant!.Update(newSku, newPrice);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedVariant = await _productVariantReadRepository.GetByIdAsync(originalVariant.Id);
        updatedVariant.Should().NotBeNull();
        updatedVariant!.Sku.Should().Be(newSku);
        updatedVariant.Price.Should().Be(299.99m);
        updatedVariant.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateAsync_ChangeVariantStatus_ShouldUpdateIsActive()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().AsActive().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Load fresh entity and deactivate
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        existingVariant!.Deactivate();
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ChangeStockQuantity_ShouldUpdateStock()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.InStock(10).BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Load fresh entity and update stock
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        existingVariant!.SetStockQuantity(25);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.StockQuantity.Should().Be(25);
    }

    [Fact]
    public async Task UpdateAsync_AddStockToVariant_ShouldIncreaseStockQuantity()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.InStock(5).BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Load fresh entity and add stock
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        existingVariant!.AddStock(10);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.StockQuantity.Should().Be(15); // 5 + 10
    }

    [Fact]
    public async Task UpdateAsync_RemoveStockFromVariant_ShouldDecreaseStockQuantity()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.InStock(20).BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Load fresh entity and remove stock
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        existingVariant!.RemoveStock(7);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.StockQuantity.Should().Be(13); // 20 - 7
    }

    [Fact]
    public async Task DeleteAsync_ExistingProductVariant_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _productVariantWriteRepository.DeleteAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProductVariant_ShouldReturnVariant()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _productVariantWriteRepository.GetByIdAsync(variant.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(variant.Id);
        result.ProductId.Should().Be(product.Id);
        result.Sku.Should().Be(variant.Sku);
        result.StockQuantity.Should().Be(variant.StockQuantity);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProductVariant_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _productVariantWriteRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySkuAsync_ExistingSku_ShouldReturnVariant()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var uniqueSku = $"GET-BY-SKU-{DateTime.Now.Ticks}";
        var variant = ProductVariantBuilder.Random()
            .WithSku(uniqueSku)
            .BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _productVariantWriteRepository.GetBySkuAsync(uniqueSku);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(variant.Id);
        result.Sku.Should().Be(uniqueSku);
    }

    [Fact]
    public async Task GetBySkuAsync_NonExistentSku_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSku = "NON-EXISTENT-SKU";

        // Act
        var result = await _productVariantWriteRepository.GetBySkuAsync(nonExistentSku);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByProductIdAsync_ProductWithVariants_ShouldReturnAllVariants()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variants = ProductVariantBuilder.CreateManyForProduct(product, 3);
        foreach (var variant in variants)
        {
            await _productVariantWriteRepository.AddAsync(variant);
        }

        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _productVariantWriteRepository.GetByProductIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.All(v => v.ProductId == product.Id).Should().BeTrue();
        result.Select(v => v.Id).Should().BeEquivalentTo(variants.Select(v => v.Id));
    }

    [Fact]
    public async Task SkuExistsAsync_ExistingSku_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var sku = $"SKU-EXISTS-{DateTime.Now.Ticks}";
        var variant = ProductVariantBuilder.Random()
            .WithSku(sku)
            .BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _productVariantWriteRepository.SkuExistsAsync(sku);

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
        var result = await _productVariantWriteRepository.SkuExistsAsync(nonExistentSku);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SkuExistsAsync_ExistingSkuWithExcludeId_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var sku = $"EXCLUDE-SKU-{DateTime.Now.Ticks}";
        var variant = ProductVariantBuilder.Random()
            .WithSku(sku)
            .BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Act - Exclude the current variant ID
        var result = await _productVariantWriteRepository.SkuExistsAsync(sku, variant.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentUpdate_SameProductVariant_ShouldHandleOptimisticConcurrency()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();
        var variantId = variant.Id;

        // Create separate service scopes to simulate true concurrent access
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var productVariantWriteRepository1 =
            scope1.ServiceProvider.GetRequiredService<IProductVariantWriteRepository>();
        var productVariantWriteRepository2 =
            scope2.ServiceProvider.GetRequiredService<IProductVariantWriteRepository>();

        // Get two instances of the same variant from separate contexts
        var variant1 = await productVariantWriteRepository1.GetByIdAsync(variantId);
        var variant2 = await productVariantWriteRepository2.GetByIdAsync(variantId);

        variant1.Should().NotBeNull();
        variant2.Should().NotBeNull();

        // Verify both variants have the same initial version
        variant1!.Version.Should().Be(variant2!.Version);

        // Modify both instances
        var sku1 = $"FIRST-UPDATE-{DateTime.Now.Ticks}";
        var sku2 = $"SECOND-UPDATE-{DateTime.Now.Ticks}";
        var price1 = Money.Create(100m, "USD").Value;
        var price2 = Money.Create(200m, "USD").Value;

        variant1.Update(sku1, price1);
        variant2.Update(sku2, price2);

        // Act & Assert
        // First update should succeed
        await productVariantWriteRepository1.UpdateAsync(variant1);
        await unitOfWork1.SaveChangesAsync();

        // Second update should fail due to concurrency conflict
        await productVariantWriteRepository2.UpdateAsync(variant2);

        var action = () => unitOfWork2.SaveChangesAsync();
        await action.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task UpdateVariantMetadata_ValidKeyValue_ShouldPersistMetadata()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.Random().BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Load fresh entity and update metadata
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        existingVariant!.UpdateMetadata("size", "Large");
        existingVariant.UpdateMetadata("color", "Red");

        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantWriteRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.Metadata.Should().ContainKey("size");
        result.Metadata["size"].Should().Be("Large");
        result.Metadata.Should().ContainKey("color");
        result.Metadata["color"].Should().Be("Red");
    }

    [Fact]
    public async Task VariantStockOperations_ShouldMaintainCorrectQuantities()
    {
        // Arrange
        await ResetDatabaseAsync();

        var product = ProductBuilder.Random().Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var variant = ProductVariantBuilder.InStock(10).BuildForProduct(product);
        await _productVariantWriteRepository.AddAsync(variant);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity
        DbContext.Entry(variant).State = EntityState.Detached;

        // Act - Perform multiple stock operations
        var existingVariant = await _productVariantWriteRepository.GetByIdAsync(variant.Id);

        // Add stock: 10 + 5 = 15
        existingVariant!.AddStock(5);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Remove stock: 15 - 3 = 12
        existingVariant.RemoveStock(3);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Set stock: 12 -> 20
        existingVariant.SetStockQuantity(20);
        await _productVariantWriteRepository.UpdateAsync(existingVariant);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _productVariantReadRepository.GetByIdAsync(variant.Id);
        result.Should().NotBeNull();
        result!.StockQuantity.Should().Be(20);
    }
}
