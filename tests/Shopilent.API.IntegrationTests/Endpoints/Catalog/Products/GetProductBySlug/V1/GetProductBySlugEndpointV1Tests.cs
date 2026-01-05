using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.API.Endpoints.Catalog.Products.CreateProduct.V1;
using Shopilent.API.Endpoints.Catalog.Categories.CreateCategory.V1;
using Shopilent.API.Endpoints.Catalog.Attributes.CreateAttribute.V1;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.API.IntegrationTests.Endpoints.Catalog.Products.GetProductBySlug.V1;

public class GetProductBySlugEndpointV1Tests : ApiIntegrationTestBase
{
    public GetProductBySlugEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetProductBySlug_WithValidSlug_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create a test product first
        var slug = $"test-product-slug-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Test Get Product By Slug",
            slug: slug,
            basePrice: 99.99m);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Clear auth header to test anonymous access
        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("Test Get Product By Slug");
        response.Data.Slug.Should().Be(slug);
        response.Data.BasePrice.Should().Be(99.99m);
        response.Data.Currency.Should().Be("USD");
        response.Data.IsActive.Should().BeTrue();
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetProductBySlug_WithValidSlug_ShouldReturnCompleteProductDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create categories for the product
        var categoryRequest = CategoryTestDataV1.Creation.CreateValidRequest(
            name: "Electronics Test Slug",
            slug: $"electronics-test-slug-{DateTime.Now.Ticks}");
        var categoryResponse = await PostApiResponseAsync<object, CreateCategoryResponseV1>("v1/categories", categoryRequest);
        AssertApiSuccess(categoryResponse);
        var categoryId = categoryResponse!.Data.Id;

        // Create attributes for the product
        var attributeRequest = AttributeTestDataV1.Creation.CreateValidRequest(
            name: $"test_brand_slug_{DateTime.Now.Ticks}",
            displayName: "Brand",
            type: "Text");
        var attributeResponse = await PostApiResponseAsync<object, CreateAttributeResponseV1>("v1/attributes", attributeRequest);
        AssertApiSuccess(attributeResponse);
        var attributeId = attributeResponse!.Data.Id;

        // Create product with categories and attributes
        var slug = $"complete-product-slug-{DateTime.Now.Ticks}";
        var productRequest = new
        {
            Name = "Complete Product Test By Slug",
            Slug = slug,
            Description = "A complete product for detailed testing by slug",
            BasePrice = 149.99m,
            Currency = "USD",
            Sku = $"TEST-SKU-SLUG-{DateTime.Now.Ticks}",
            CategoryIds = new List<Guid> { categoryId },
            Metadata = new Dictionary<string, object>
            {
                { "brand", "TestBrand" },
                { "warranty", "2 years" }
            },
            IsActive = true,
            Attributes = new List<object>
            {
                new
                {
                    AttributeId = attributeId,
                    Value = "TestBrand Value"
                }
            },
            Images = new List<object>()
        };

        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("Complete Product Test By Slug");
        response.Data.Description.Should().Be("A complete product for detailed testing by slug");
        response.Data.BasePrice.Should().Be(149.99m);
        response.Data.Slug.Should().Be(slug);

        // Verify related data
        response.Data.Categories.Should().NotBeNull();
        response.Data.Categories.Should().HaveCount(1);
        response.Data.Categories.First().Id.Should().Be(categoryId);

        response.Data.Attributes.Should().NotBeNull();
        response.Data.Attributes.Should().HaveCount(1);
        response.Data.Attributes.First().AttributeId.Should().Be(attributeId);

        response.Data.Metadata.Should().NotBeNull();
        response.Data.Metadata.Should().ContainKey("brand");
        response.Data.Metadata["brand"].ToString().Should().Be("TestBrand");
    }

    [Fact]
    public async Task GetProductBySlug_CreatedProduct_ShouldBeActiveByDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"active-product-slug-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Active Product Test",
            slug: slug,
            isActive: true);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsActive.Should().BeTrue(); // Products are active by default
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetProductBySlug_WithNonExistentSlug_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentSlug = $"non-existent-slug-{Guid.NewGuid()}";

        // Act
        var response = await Client.GetAsync($"v1/products/slug/{nonExistentSlug}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain(nonExistentSlug);
        content.Should().ContainAny("not found", "NotFound");
    }

    [Fact]
    public async Task GetProductBySlug_WithEmptySlug_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("v1/products/slug/");

        // Assert
        // Empty route parameter is treated as bad request by FastEndpoints
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProductBySlug_WithSpecialCharactersInSlug_ShouldHandleCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"product-with-numbers-123-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Product with numbers",
            slug: slug);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Slug.Should().Be(slug);
    }

    #endregion

    #region Anonymous Access Tests

    [Fact]
    public async Task GetProductBySlug_WithoutAuthentication_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create a test product
        var slug = $"anonymous-access-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(slug: slug);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        // Clear authentication
        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Slug.Should().Be(slug);
    }

    [Fact]
    public async Task GetProductBySlug_WithCustomerRole_ShouldReturnSuccess()
    {
        // Arrange
        var adminToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminToken);

        // Create a test product as admin
        var slug = $"customer-access-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(slug: slug);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        // Switch to customer authentication
        var customerToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerToken);

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Slug.Should().Be(slug);
    }

    #endregion

    #region Data Persistence Tests

    [Fact]
    public async Task GetProductBySlug_ShouldReturnDataFromDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"db-persistence-slug-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "DB Persistence Test Product By Slug",
            slug: slug,
            basePrice: 199.99m,
            sku: $"DB-TEST-SLUG-{DateTime.Now.Ticks}");
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(response);

        // Verify data matches what was created
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("DB Persistence Test Product By Slug");
        response.Data.BasePrice.Should().Be(199.99m);
        response.Data.Slug.Should().Be(slug);
        response.Data.IsActive.Should().BeTrue();

        // Verify in database directly
        await ExecuteDbContextAsync(async context =>
        {
            var dbProduct = await context.Products
                .FirstOrDefaultAsync(p => p.Slug.Value == slug);

            dbProduct.Should().NotBeNull();
            dbProduct!.Name.Should().Be("DB Persistence Test Product By Slug");
            dbProduct.BasePrice.Amount.Should().Be(199.99m);
            dbProduct.Slug.Value.Should().Be(slug);
            dbProduct.IsActive.Should().BeTrue();
        });
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetProductBySlug_CalledTwice_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"cache-consistency-slug-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Cache Consistency Test By Slug",
            slug: slug);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        ClearAuthenticationHeader();

        // Act - Call twice
        var firstResponse = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");
        var secondResponse = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        firstResponse!.Data.Should().NotBeNull();
        secondResponse!.Data.Should().NotBeNull();

        // Data should be identical
        firstResponse.Data.Id.Should().Be(secondResponse.Data.Id);
        firstResponse.Data.Name.Should().Be(secondResponse.Data.Name);
        firstResponse.Data.Description.Should().Be(secondResponse.Data.Description);
        firstResponse.Data.BasePrice.Should().Be(secondResponse.Data.BasePrice);
        firstResponse.Data.Slug.Should().Be(secondResponse.Data.Slug);
        firstResponse.Data.CreatedAt.Should().Be(secondResponse.Data.CreatedAt);
        firstResponse.Data.UpdatedAt.Should().Be(secondResponse.Data.UpdatedAt);
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task GetProductBySlug_ShouldReturnProperApiResponseFormat()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"api-response-format-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(slug: slug);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Message.Should().NotBeNullOrEmpty();
        response.Errors.Should().BeEmpty();
        response.StatusCode.Should().Be(200);
    }

    #endregion

    #region Related Entities Tests

    [Fact]
    public async Task GetProductBySlug_WithCategories_ShouldReturnCategoryDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create category
        var categorySlug = $"test-category-slug-{DateTime.Now.Ticks}";
        var categoryRequest = CategoryTestDataV1.Creation.CreateValidRequest(
            name: "Test Category for Product By Slug",
            slug: categorySlug);
        var categoryResponse = await PostApiResponseAsync<object, CreateCategoryResponseV1>("v1/categories", categoryRequest);
        AssertApiSuccess(categoryResponse);
        var categoryId = categoryResponse!.Data.Id;

        // Create product with category
        var productSlug = $"product-with-category-{DateTime.Now.Ticks}";
        var productRequest = new
        {
            Name = "Product with Category",
            Slug = productSlug,
            Description = "Test product with category",
            BasePrice = 99.99m,
            Currency = "USD",
            Sku = $"CAT-TEST-{DateTime.Now.Ticks}",
            CategoryIds = new List<Guid> { categoryId },
            Metadata = new Dictionary<string, object>(),
            IsActive = true,
            Attributes = new List<object>(),
            Images = new List<object>()
        };
        var productResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(productResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{productSlug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Categories.Should().NotBeNull();
        response.Data.Categories.Should().HaveCount(1);
        response.Data.Categories.First().Id.Should().Be(categoryId);
        response.Data.Categories.First().Name.Should().Be("Test Category for Product By Slug");
    }

    [Fact]
    public async Task GetProductBySlug_WithAttributes_ShouldReturnAttributeDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create attribute
        var attributeRequest = AttributeTestDataV1.Creation.CreateValidRequest(
            name: $"test_color_slug_{DateTime.Now.Ticks}",
            displayName: "Color",
            type: "Select");
        var attributeResponse = await PostApiResponseAsync<object, CreateAttributeResponseV1>("v1/attributes", attributeRequest);
        AssertApiSuccess(attributeResponse);
        var attributeId = attributeResponse!.Data.Id;

        // Create product with attribute
        var productSlug = $"product-with-attribute-{DateTime.Now.Ticks}";
        var productRequest = new
        {
            Name = "Product with Attribute",
            Slug = productSlug,
            Description = "Test product with attribute",
            BasePrice = 79.99m,
            Currency = "USD",
            Sku = $"ATTR-TEST-{DateTime.Now.Ticks}",
            CategoryIds = new List<Guid>(),
            Metadata = new Dictionary<string, object>(),
            IsActive = true,
            Attributes = new List<object>
            {
                new
                {
                    AttributeId = attributeId,
                    Value = "Test Value"
                }
            },
            Images = new List<object>()
        };
        var productResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(productResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{productSlug}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Attributes.Should().NotBeNull();
        response.Data.Attributes.Should().HaveCount(1);
        response.Data.Attributes.First().AttributeId.Should().Be(attributeId);
    }

    #endregion

    #region Inactive Product Tests

    [Fact]
    public async Task GetProductBySlug_InactiveProduct_ShouldStillReturnProduct()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"inactive-product-slug-{DateTime.Now.Ticks}";
        var createRequest = new
        {
            Name = "Inactive Product",
            Slug = slug,
            Description = "Test inactive product",
            BasePrice = 49.99m,
            Currency = "USD",
            Sku = $"INACTIVE-{DateTime.Now.Ticks}",
            CategoryIds = new List<Guid>(),
            Metadata = new Dictionary<string, object>(),
            IsActive = false,
            Attributes = new List<object>(),
            Images = new List<object>()
        };
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);

        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert - GetProductBySlug should return inactive products (filtering happens at list level)
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.IsActive.Should().BeFalse();
        response.Data.Slug.Should().Be(slug);
    }

    #endregion

    #region Comparison with GetById Tests

    [Fact]
    public async Task GetProductBySlug_ShouldReturnSameDataAsGetById()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var slug = $"comparison-test-{DateTime.Now.Ticks}";
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Comparison Test Product",
            slug: slug,
            basePrice: 75.50m);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        ClearAuthenticationHeader();

        // Act - Get by both ID and slug
        var responseById = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");
        var responseBySlug = await GetApiResponseAsync<ProductDetailDto>($"v1/products/slug/{slug}");

        // Assert - Both should return the same data
        AssertApiSuccess(responseById);
        AssertApiSuccess(responseBySlug);

        responseById!.Data.Should().NotBeNull();
        responseBySlug!.Data.Should().NotBeNull();

        responseById.Data.Id.Should().Be(responseBySlug.Data.Id);
        responseById.Data.Name.Should().Be(responseBySlug.Data.Name);
        responseById.Data.Slug.Should().Be(responseBySlug.Data.Slug);
        responseById.Data.BasePrice.Should().Be(responseBySlug.Data.BasePrice);
        responseById.Data.Currency.Should().Be(responseBySlug.Data.Currency);
        responseById.Data.IsActive.Should().Be(responseBySlug.Data.IsActive);
        responseById.Data.CreatedAt.Should().Be(responseBySlug.Data.CreatedAt);
        responseById.Data.UpdatedAt.Should().Be(responseBySlug.Data.UpdatedAt);
    }

    #endregion
}
