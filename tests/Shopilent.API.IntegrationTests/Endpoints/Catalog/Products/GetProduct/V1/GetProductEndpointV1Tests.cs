using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.API.Endpoints.Catalog.Products.CreateProduct.V1;
using Shopilent.API.Endpoints.Catalog.Categories.CreateCategory.V1;
using Shopilent.API.Endpoints.Catalog.Attributes.CreateAttribute.V1;
using Shopilent.Domain.Catalog.DTOs;

namespace Shopilent.API.IntegrationTests.Endpoints.Catalog.Products.GetProduct.V1;

public class GetProductEndpointV1Tests : ApiIntegrationTestBase
{
    public GetProductEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetProduct_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create a test product first
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Test Get Product",
            slug: "test-get-product",
            basePrice: 99.99m);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("Test Get Product");
        response.Data.Slug.Should().Be("test-get-product");
        response.Data.BasePrice.Should().Be(99.99m);
        response.Data.Currency.Should().Be("USD");
        response.Data.IsActive.Should().BeTrue();
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetProduct_WithValidId_ShouldReturnCompleteProductDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create categories for the product
        var categoryRequest = CategoryTestDataV1.Creation.CreateValidRequest(
            name: "Electronics Test",
            slug: "electronics-test");
        var categoryResponse = await PostApiResponseAsync<object, CreateCategoryResponseV1>("v1/categories", categoryRequest);
        AssertApiSuccess(categoryResponse);
        var categoryId = categoryResponse!.Data.Id;

        // Create attributes for the product
        var attributeRequest = AttributeTestDataV1.Creation.CreateValidRequest(
            name: "test_brand",
            displayName: "Brand",
            type: "Text");
        var attributeResponse = await PostApiResponseAsync<object, CreateAttributeResponseV1>("v1/attributes", attributeRequest);
        AssertApiSuccess(attributeResponse);
        var attributeId = attributeResponse!.Data.Id;

        // Create product with categories and attributes
        var productRequest = new
        {
            Name = "Complete Product Test",
            Slug = "complete-product-test",
            Description = "A complete product for detailed testing",
            BasePrice = 149.99m,
            Currency = "USD",
            Sku = "TEST-SKU-001",
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

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("Complete Product Test");
        response.Data.Description.Should().Be("A complete product for detailed testing");
        response.Data.BasePrice.Should().Be(149.99m);
        response.Data.Sku.Should().Be("TEST-SKU-001");

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
    public async Task GetProduct_CreatedProduct_ShouldBeActiveByDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Active Product Test",
            slug: "active-product-test",
            isActive: true);
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsActive.Should().BeTrue(); // Products are active by default
    }

    [Fact]
    public async Task GetProduct_WithMultipleCurrencies_ShouldReturnCorrectCurrency()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.CurrencyTests.CreateRequestWithEUR();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Currency.Should().Be("EUR");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetProduct_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"v1/products/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain(nonExistentId.ToString());
        content.Should().ContainAny("not found", "NotFound");
    }

    [Fact]
    public async Task GetProduct_WithInvalidGuidFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync("v1/products/invalid-guid-format");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProduct_WithEmptyGuid_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync($"v1/products/{Guid.Empty}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetProduct_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create a test product
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Clear authentication
        ClearAuthenticationHeader();

        // Act
        var response = await Client.GetAsync($"v1/products/{productId}");

        // Assert - Endpoint requires admin/manager authentication
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProduct_WithCustomerRole_ShouldReturnForbidden()
    {
        // Arrange
        var adminToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminToken);

        // Create a test product as admin
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Switch to customer authentication
        var customerToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerToken);

        // Act
        var response = await Client.GetAsync($"v1/products/{productId}");

        // Assert - Customers should not have access to admin endpoint
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProduct_WithAdminRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create a test product
        var createRequest = ProductTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act - Admin should have access
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
    }

    #endregion

    #region Unicode and Special Characters Tests

    [Fact]
    public async Task GetProduct_WithUnicodeCharacters_ShouldReturnCorrectData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.EdgeCases.CreateRequestWithUnicodeCharacters();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Name.Should().Be("Café Münchën Product™");
        response.Data.Description.Should().Contain("Ürünümüz için açıklama");
    }

    [Fact]
    public async Task GetProduct_WithSpecialCharacters_ShouldReturnCorrectData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.EdgeCases.CreateRequestWithSpecialCharacters();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Name.Should().Be("Product-With_Special.Chars@123");
        response.Data.Description.Should().Contain("special characters");
    }

    #endregion

    #region Data Persistence Tests

    [Fact]
    public async Task GetProduct_ShouldReturnDataFromDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "DB Persistence Test Product",
            slug: "db-persistence-test-product",
            basePrice: 199.99m,
            sku: "DB-TEST-001");
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);

        // Verify data matches what was created
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(productId);
        response.Data.Name.Should().Be("DB Persistence Test Product");
        response.Data.BasePrice.Should().Be(199.99m);
        response.Data.Sku.Should().Be("DB-TEST-001");
        response.Data.IsActive.Should().BeTrue();

        // Verify in database directly
        await ExecuteDbContextAsync(async context =>
        {
            var dbProduct = await context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            dbProduct.Should().NotBeNull();
            dbProduct!.Name.Should().Be("DB Persistence Test Product");
            dbProduct.BasePrice.Amount.Should().Be(199.99m);
            dbProduct.Sku.Should().Be("DB-TEST-001");
            dbProduct.IsActive.Should().BeTrue();
        });
    }

    #endregion

    #region Complex Metadata Tests

    [Fact]
    public async Task GetProduct_WithComplexMetadata_ShouldReturnCompleteData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.EdgeCases.CreateRequestWithComplexMetadata();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Metadata.Should().NotBeNull();
        response.Data.Metadata.Should().ContainKey("brand");
        response.Data.Metadata.Should().ContainKey("manufacturer");
        response.Data.Metadata.Should().ContainKey("warranty_months");
        response.Data.Metadata.Should().ContainKey("featured");
    }

    [Fact]
    public async Task GetProduct_WithEmptyCollections_ShouldReturnEmptyLists()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.EdgeCases.CreateRequestWithEmptyCollections();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Categories.Should().NotBeNull();
        response.Data.Categories.Should().BeEmpty();
        response.Data.Attributes.Should().NotBeNull();
        response.Data.Attributes.Should().BeEmpty();
        response.Data.Images.Should().NotBeNull();
        response.Data.Images.Should().BeEmpty();
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetProduct_CalledTwice_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: "Cache Consistency Test",
            slug: "cache-consistency-test");
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act - Call twice
        var firstResponse = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");
        var secondResponse = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

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
        firstResponse.Data.Sku.Should().Be(secondResponse.Data.Sku);
        firstResponse.Data.Slug.Should().Be(secondResponse.Data.Slug);
        firstResponse.Data.CreatedAt.Should().Be(secondResponse.Data.CreatedAt);
        firstResponse.Data.UpdatedAt.Should().Be(secondResponse.Data.UpdatedAt);
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task GetProduct_ShouldReturnProperApiResponseFormat()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Message.Should().NotBeNullOrEmpty();
        response.Errors.Should().BeEmpty();
        response.StatusCode.Should().Be(200);
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public async Task GetProduct_WithMaximumNameLength_ShouldReturnCorrectData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.BoundaryTests.CreateRequestWithMaximumNameLength();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Name.Should().HaveLength(255);
        response.Data.Name.Should().Be(new string('A', 255));
    }

    [Fact]
    public async Task GetProduct_WithMaximumDescriptionLength_ShouldReturnCorrectData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.BoundaryTests.CreateRequestWithMaximumDescriptionLength();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Description.Should().HaveLength(2000);
        response.Data.Description.Should().Be(new string('D', 2000));
    }

    [Fact]
    public async Task GetProduct_WithZeroPrice_ShouldReturnCorrectData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.BoundaryTests.CreateRequestWithZeroPrice();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.BasePrice.Should().Be(0m);
    }

    #endregion

    #region Multiple Products Integration Test

    [Fact]
    public async Task GetProduct_MultipleProducts_ShouldReturnCorrectIndividualData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var testProducts = new[]
        {
            ("Product A", 10.00m, "PROD-A"),
            ("Product B", 20.00m, "PROD-B"),
            ("Product C", 30.00m, "PROD-C")
        };

        var productIds = new List<Guid>();

        // Create all products
        foreach (var (name, price, sku) in testProducts)
        {
            var request = ProductTestDataV1.Creation.CreateValidRequest(
                name: name,
                basePrice: price,
                sku: sku);
            var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", request);
            AssertApiSuccess(createResponse);
            productIds.Add(createResponse!.Data.Id);
        }

        // Act & Assert - Retrieve and verify each product
        for (int i = 0; i < testProducts.Length; i++)
        {
            var (expectedName, expectedPrice, expectedSku) = testProducts[i];
            var productId = productIds[i];

            var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");
            AssertApiSuccess(response);

            response!.Data.Should().NotBeNull();
            response.Data.Id.Should().Be(productId);
            response.Data.Name.Should().Be(expectedName);
            response.Data.BasePrice.Should().Be(expectedPrice);
            response.Data.Sku.Should().Be(expectedSku);
            response.Data.IsActive.Should().BeTrue();
        }
    }

    #endregion

    #region Related Entities Tests

    [Fact]
    public async Task GetProduct_WithCategories_ShouldReturnCategoryDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create category
        var categoryRequest = CategoryTestDataV1.Creation.CreateValidRequest(
            name: "Test Category for Product",
            slug: "test-category-product");
        var categoryResponse = await PostApiResponseAsync<object, CreateCategoryResponseV1>("v1/categories", categoryRequest);
        AssertApiSuccess(categoryResponse);
        var categoryId = categoryResponse!.Data.Id;

        // Create product with category
        var productRequest = ProductTestDataV1.Creation.CreateProductWithCategories(new List<Guid> { categoryId });
        var productResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(productResponse);
        var productId = productResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Categories.Should().NotBeNull();
        response.Data.Categories.Should().HaveCount(1);
        response.Data.Categories.First().Id.Should().Be(categoryId);
        response.Data.Categories.First().Name.Should().Be("Test Category for Product");
    }

    [Fact]
    public async Task GetProduct_WithAttributes_ShouldReturnAttributeDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create attribute
        var attributeRequest = AttributeTestDataV1.Creation.CreateValidRequest(
            name: "test_color",
            displayName: "Color",
            type: "Select");
        var attributeResponse = await PostApiResponseAsync<object, CreateAttributeResponseV1>("v1/attributes", attributeRequest);
        AssertApiSuccess(attributeResponse);
        var attributeId = attributeResponse!.Data.Id;

        // Create product with attribute
        var productRequest = ProductTestDataV1.Creation.CreateProductWithAttributes(new List<Guid> { attributeId });
        var productResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(productResponse);
        var productId = productResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Attributes.Should().NotBeNull();
        response.Data.Attributes.Should().HaveCount(1);
        response.Data.Attributes.First().AttributeId.Should().Be(attributeId);
    }

    #endregion

    #region Inactive Product Tests

    [Fact]
    public async Task GetProduct_InactiveProduct_ShouldStillReturnProduct()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = ProductTestDataV1.EdgeCases.CreateInactiveProductRequest();
        var createResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", createRequest);
        AssertApiSuccess(createResponse);
        var productId = createResponse!.Data.Id;

        // Act
        var response = await GetApiResponseAsync<ProductDetailDto>($"v1/products/{productId}");

        // Assert - Admin endpoint should return inactive products (allows admin to manage all products)
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.IsActive.Should().BeFalse();
    }

    #endregion

}
