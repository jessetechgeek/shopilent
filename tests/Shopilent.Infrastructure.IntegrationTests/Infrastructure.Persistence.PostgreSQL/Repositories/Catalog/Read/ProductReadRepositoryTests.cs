using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Models;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Read;

[Collection("IntegrationTests")]
public class ProductReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IProductWriteRepository _productWriteRepository = null!;
    private IProductReadRepository _productReadRepository = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;
    private IProductVariantWriteRepository _productVariantWriteRepository = null!;

    public ProductReadRepositoryTests(IntegrationTestFixture integrationTestFixture) : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _productWriteRepository = GetService<IProductWriteRepository>();
        _productReadRepository = GetService<IProductReadRepository>();
        _categoryWriteRepository = GetService<ICategoryWriteRepository>();
        _productVariantWriteRepository = GetService<IProductVariantWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ShouldReturnProductDto()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetByIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be(product.Name);
        result.Description.Should().Be(product.Description);
        result.Slug.Should().Be(product.Slug.Value);
        result.IsActive.Should().Be(product.IsActive);
        result.BasePrice.Should().Be(product.BasePrice.Amount);
        result.Currency.Should().Be(product.BasePrice.Currency);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProduct_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _productReadRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailByIdAsync_ExistingProduct_ShouldReturnProductDetailDto()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailByIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be(product.Name);
        result.Description.Should().Be(product.Description);
        result.Slug.Should().Be(product.Slug.Value);
        result.IsActive.Should().Be(product.IsActive);
        result.BasePrice.Should().Be(product.BasePrice.Amount);
        result.Currency.Should().Be(product.BasePrice.Currency);
        result.CreatedAt.Should().BeCloseTo(product.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(product.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetDetailByIdAsync_NonExistentProduct_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _productReadRepository.GetDetailByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_ExistingSlug_ShouldReturnProductDto()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"test-product-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetBySlugAsync(product.Slug.Value);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Slug.Should().Be(product.Slug.Value);
        result.Name.Should().Be(product.Name);
    }

    [Fact]
    public async Task GetBySlugAsync_NonExistentSlug_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSlug = "non-existent-product";

        // Act
        var result = await _productReadRepository.GetBySlugAsync(nonExistentSlug);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlug_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"existing-product-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.SlugExistsAsync(product.Slug.Value);

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
        var result = await _productReadRepository.SlugExistsAsync(nonExistentSlug);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlugWithExcludeId_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"exclude-test-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act - Exclude the current product ID
        var result = await _productReadRepository.SlugExistsAsync(product.Slug.Value, product.Id);

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
        // Update with SKU - need to access internal method or create via domain method
        product.Update(product.Name, product.Slug, product.BasePrice, product.Description, sku);
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.SkuExistsAsync(sku);

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
        var result = await _productReadRepository.SkuExistsAsync(nonExistentSku);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdsAsync_ExistingIds_ShouldReturnProducts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var products = ProductBuilder.CreateMany(3);
        foreach (var product in products)
        {
            await _productWriteRepository.AddAsync(product);
        }

        await _unitOfWork.CommitAsync();

        var productIds = products.Select(p => p.Id).ToList();

        // Act
        var result = await _productReadRepository.GetByIdsAsync(productIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Select(p => p.Id).Should().BeEquivalentTo(productIds);
    }

    [Fact]
    public async Task GetByIdsAsync_MixedExistingAndNonExistentIds_ShouldReturnOnlyExistingProducts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        var ids = new List<Guid> { product.Id, Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var result = await _productReadRepository.GetByIdsAsync(ids);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task GetByCategoryAsync_ProductsInCategory_ShouldReturnProducts()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create category first
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Create products and assign to category
        var products = ProductBuilder.CreateMany(2);
        foreach (var product in products)
        {
            product.AddCategory(category.Id);
            await _productWriteRepository.AddAsync(product);
        }

        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetByCategoryAsync(category.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(p => products.Any(prod => prod.Id == p.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetByCategoryAsync_NoProductsInCategory_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetByCategoryAsync(category.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_MatchingProductName_ShouldReturnProducts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var searchTerm = "Unique";
        var product1 = ProductBuilder.Random()
            .WithName($"{searchTerm} Product 1")
            .Build();
        var product2 = ProductBuilder.Random()
            .WithName($"Another {searchTerm} Product")
            .Build();
        var product3 = ProductBuilder.Random()
            .WithName("Different Product")
            .Build();

        await _productWriteRepository.AddAsync(product1);
        await _productWriteRepository.AddAsync(product2);
        await _productWriteRepository.AddAsync(product3);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.SearchAsync(searchTerm);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == product1.Id);
        result.Should().Contain(p => p.Id == product2.Id);
        result.Should().NotContain(p => p.Id == product3.Id);
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random().Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.SearchAsync("NonExistentSearchTerm");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAllAsync_HasProducts_ShouldReturnAllProducts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var products = ProductBuilder.CreateMany(5);
        foreach (var product in products)
        {
            await _productWriteRepository.AddAsync(product);
        }

        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task ListAllAsync_ActiveAndInactiveProducts_ShouldReturnAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var activeProduct = ProductBuilder.Random().AsActive().Build();
        var inactiveProduct = ProductBuilder.Random().AsInactive().Build();

        await _productWriteRepository.AddAsync(activeProduct);
        await _productWriteRepository.AddAsync(inactiveProduct);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == activeProduct.Id && p.IsActive);
        result.Should().Contain(p => p.Id == inactiveProduct.Id && !p.IsActive);
    }

    [Fact]
    public async Task GetProductDetailDataTableAsync_WithPagination_ShouldReturnPaginatedResults()
    {
        // Arrange
        await ResetDatabaseAsync();
        var products = ProductBuilder.CreateMany(10);
        foreach (var product in products)
        {
            await _productWriteRepository.AddAsync(product);
        }

        await _unitOfWork.CommitAsync();

        var request = new DataTableRequest
        {
            Start = 0,
            Length = 5,
            Search = new DataTableSearch { Value = "" },
            Order = new List<DataTableOrder> { new DataTableOrder { Column = 0, Dir = "asc" } },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "name", Searchable = true, Orderable = true }
            }
        };

        // Act
        var result = await _productReadRepository.GetProductDetailDataTableAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RecordsFiltered.Should().Be(10);
        result.RecordsTotal.Should().Be(10);
        result.Data.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetProductDetailDataTableAsync_WithSearch_ShouldReturnFilteredResults()
    {
        // Arrange
        await ResetDatabaseAsync();
        var searchableProduct = ProductBuilder.Random()
            .WithName("Searchable Product")
            .Build();
        var regularProduct = ProductBuilder.Random()
            .WithName("Regular Product")
            .Build();

        await _productWriteRepository.AddAsync(searchableProduct);
        await _productWriteRepository.AddAsync(regularProduct);
        await _unitOfWork.CommitAsync();

        var request = new DataTableRequest
        {
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "Searchable" },
            Order = new List<DataTableOrder> { new DataTableOrder { Column = 0, Dir = "asc" } },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "name", Searchable = true, Orderable = true }
            }
        };

        // Act
        var result = await _productReadRepository.GetProductDetailDataTableAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RecordsTotal.Should().Be(2); // Total before filtering
        result.RecordsFiltered.Should().Be(1); // After filtering
        result.Data.Should().HaveCount(1);
        result.Data.First().Name.Should().Be("Searchable Product");
    }

    [Fact]
    public async Task GetDetailBySlugAsync_ExistingProduct_ShouldReturnProductDetailDto()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"test-product-slug-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.Name.Should().Be(product.Name);
        result.Description.Should().Be(product.Description);
        result.Slug.Should().Be(product.Slug.Value);
        result.IsActive.Should().Be(product.IsActive);
        result.BasePrice.Should().Be(product.BasePrice.Amount);
        result.Currency.Should().Be(product.BasePrice.Currency);
        result.CreatedAt.Should().BeCloseTo(product.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(product.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetDetailBySlugAsync_NonExistentSlug_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentSlug = $"non-existent-slug-{DateTime.Now.Ticks}";

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(nonExistentSlug);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailBySlugAsync_WithCategories_ShouldReturnProductWithCategories()
    {
        // Arrange
        await ResetDatabaseAsync();
        var category = CategoryBuilder.Random().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product = ProductBuilder.Random()
            .WithSlug($"product-with-cat-{DateTime.Now.Ticks}")
            .Build();
        product.AddCategory(category.Id);
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert
        result.Should().NotBeNull();
        result!.Categories.Should().HaveCount(1);
        result.Categories.First().Id.Should().Be(category.Id);
        result.Categories.First().Name.Should().Be(category.Name);
    }

    [Fact]
    public async Task GetDetailBySlugAsync_WithImages_ShouldReturnProductWithImages()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"product-with-images-{DateTime.Now.Ticks}")
            .Build();

        var image1 = ProductImage.Create("images/product1.jpg", "thumb/product1.jpg", "Product Image 1", true, 1).Value;
        var image2 = ProductImage.Create("images/product2.jpg", "thumb/product2.jpg", "Product Image 2", false, 2)
            .Value;
        product.AddImage(image1);
        product.AddImage(image2);

        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert
        result.Should().NotBeNull();
        result!.Images.Should().HaveCount(2);
        result.Images.Should().Contain(img => img.ImageKey == "images/product1.jpg" && img.IsDefault == true);
        result.Images.Should().Contain(img => img.ImageKey == "images/product2.jpg" && img.IsDefault == false);
    }

    [Fact]
    public async Task GetDetailBySlugAsync_WithVariants_ShouldReturnProductWithVariants()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"product-with-variants-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        var variant1 = ProductVariantBuilder.Random()
            .WithSku($"VAR1-{DateTime.Now.Ticks}")
            .BuildForProduct(product);
        var variant2 = ProductVariantBuilder.Random()
            .WithSku($"VAR2-{DateTime.Now.Ticks}")
            .BuildForProduct(product);

        await _productVariantWriteRepository.AddAsync(variant1);
        await _productVariantWriteRepository.AddAsync(variant2);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert
        result.Should().NotBeNull();
        result!.Variants.Should().HaveCount(2);
        result.Variants.Should().Contain(v => v.Sku == variant1.Sku);
        result.Variants.Should().Contain(v => v.Sku == variant2.Sku);
    }

    [Fact]
    public async Task GetDetailBySlugAsync_InactiveProduct_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"inactive-product-{DateTime.Now.Ticks}")
            .AsInactive()
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert - Inactive products should not be returned via slug (public endpoint)
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailBySlugAsync_ActiveProductWithInactiveVariants_ShouldReturnProductWithOnlyActiveVariants()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .WithSlug($"product-mixed-variants-{DateTime.Now.Ticks}")
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Create active and inactive variants - ProductVariant is now a separate aggregate
        var activeVariant = ProductVariantBuilder.Random()
            .WithSku($"ACTIVE-VAR-{DateTime.Now.Ticks}")
            .BuildForProduct(product);
        var inactiveVariant = ProductVariantBuilder.Random()
            .WithSku($"INACTIVE-VAR-{DateTime.Now.Ticks}")
            .AsInactive()
            .BuildForProduct(product);

        await _productVariantWriteRepository.AddAsync(activeVariant);
        await _productVariantWriteRepository.AddAsync(inactiveVariant);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert - Only active variants should be returned
        result.Should().NotBeNull();
        result!.Variants.Should().HaveCount(1);
        result.Variants.Should().Contain(v => v.Sku == activeVariant.Sku);
        result.Variants.Should().NotContain(v => v.Sku == inactiveVariant.Sku);
        result.Variants.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetDetailBySlugAsync_ActiveProductWithInactiveCategories_ShouldReturnProductWithOnlyActiveCategories()
    {
        // Arrange
        await ResetDatabaseAsync();
        var activeCategory = CategoryBuilder.Random()
            .WithSlug($"active-cat-{DateTime.Now.Ticks}")
            .Build();
        var categoryToDeactivate = CategoryBuilder.Random()
            .WithSlug($"will-deactivate-cat-{DateTime.Now.Ticks}")
            .Build();

        await _categoryWriteRepository.AddAsync(activeCategory);
        await _categoryWriteRepository.AddAsync(categoryToDeactivate);
        await _unitOfWork.CommitAsync();

        var product = ProductBuilder.Random()
            .WithSlug($"product-mixed-cats-{DateTime.Now.Ticks}")
            .Build();
        product.AddCategory(activeCategory.Id);
        product.AddCategory(categoryToDeactivate.Id);
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Deactivate category AFTER it was added to product (simulates business scenario)
        categoryToDeactivate.Deactivate();
        await _categoryWriteRepository.UpdateAsync(categoryToDeactivate);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _productReadRepository.GetDetailBySlugAsync(product.Slug.Value);

        // Assert - Only active categories should be returned
        result.Should().NotBeNull();
        result!.Categories.Should().HaveCount(1);
        result.Categories.Should().Contain(c => c.Id == activeCategory.Id);
        result.Categories.Should().NotContain(c => c.Id == categoryToDeactivate.Id);
        result.Categories.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetDetailByIdAsync_InactiveProduct_ShouldStillReturnProduct()
    {
        // Arrange
        await ResetDatabaseAsync();
        var product = ProductBuilder.Random()
            .AsInactive()
            .Build();
        await _productWriteRepository.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Act - GetDetailByIdAsync is for admin use and should return inactive products
        var result = await _productReadRepository.GetDetailByIdAsync(product.Id);

        // Assert - Admin endpoint should return inactive products
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
        result.Id.Should().Be(product.Id);
    }
}
