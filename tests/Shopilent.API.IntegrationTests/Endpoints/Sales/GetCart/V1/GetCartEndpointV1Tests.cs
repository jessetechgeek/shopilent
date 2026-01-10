using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.Common.Models;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Endpoints.Catalog.Products.CreateProduct.V1;
using Shopilent.API.Endpoints.Catalog.Categories.CreateCategory.V1;
using Shopilent.Application.Features.Sales.Commands.AddItemToCart.V1;
using Shopilent.Domain.Sales.DTOs;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.GetCart.V1;

public class GetCartEndpointV1Tests : ApiIntegrationTestBase
{
    public GetCartEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetCart_WithValidCartId_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a product first
        var (productId, variantId) = await CreateTestProductAsync();

        // Add item to cart to create a cart
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(
            cartId: null,
            productId: productId,
            variantId: variantId,
            quantity: 2);

        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Act
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(cartId);
        response.Data.Items.Should().NotBeEmpty();
        response.Data.Items.Should().HaveCount(1);
        response.Data.TotalItems.Should().Be(2);
        response.Data.TotalAmount.Should().BeGreaterThan(0);
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Message.Should().Be("Cart retrieved successfully");
    }

    [Fact]
    public async Task GetCart_WithAuthenticatedUserWithoutCartId_ShouldReturnUserCart()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a product first
        var (productId, variantId) = await CreateTestProductAsync();

        // Add item to cart to create a cart for this user
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(
            cartId: null,
            productId: productId,
            variantId: variantId,
            quantity: 3);

        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        // Act - Get cart without specifying cart ID
        var response = await GetApiResponseAsync<CartDto>("v1/cart");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Items.Should().NotBeEmpty();
        response.Data.TotalItems.Should().Be(3);
        response.Data.UserId.Should().NotBeNull();
        response.Message.Should().Be("Cart retrieved successfully");
    }

    [Fact]
    public async Task GetCart_WithEmptyCart_ShouldReturnNull()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act - Try to get cart when user has no cart
        var response = await GetApiResponseAsync<CartDto>("v1/cart");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().BeNull();
        response.Message.Should().Be("No cart found");
    }

    [Fact]
    public async Task GetCart_WithMultipleItems_ShouldReturnAllItems()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple products
        var product1 = await CreateTestProductAsync();
        var product2 = await CreateTestProductAsync();
        var product3 = await CreateTestProductAsync();

        // Add items to cart
        var addRequest1 = CartTestDataV1.Creation.CreateValidRequest(null, product1.ProductId, product1.VariantId, 1);
        var addRequest2 = CartTestDataV1.Creation.CreateValidRequest(null, product2.ProductId, product2.VariantId, 2);
        var addRequest3 = CartTestDataV1.Creation.CreateValidRequest(null, product3.ProductId, product3.VariantId, 3);

        await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addRequest1);
        await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addRequest2);
        await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addRequest3);

        // Act
        var response = await GetApiResponseAsync<CartDto>("v1/cart");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Items.Should().HaveCount(3);
        response.Data.TotalItems.Should().Be(6); // 1 + 2 + 3
        response.Data.Items.Should().OnlyHaveUniqueItems(item => item.Id);

        // Verify each item has correct structure
        foreach (var item in response.Data.Items)
        {
            item.Id.Should().NotBeEmpty();
            item.CartId.Should().Be(response.Data.Id);
            item.ProductId.Should().NotBeEmpty();
            item.ProductName.Should().NotBeNullOrEmpty();
            item.UnitPrice.Should().BeGreaterThan(0);
            item.Quantity.Should().BeGreaterThan(0);
            item.TotalPrice.Should().Be(item.UnitPrice * item.Quantity);
        }
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task GetCart_WithoutAuthentication_WithValidCartId_ShouldReturnSuccess()
    {
        // Arrange - Create cart with authenticated user first
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Clear authentication
        ClearAuthenticationHeader();

        // Act - Try to get cart without authentication but with cart ID
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert - Endpoint allows anonymous access
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetCart_WithoutAuthenticationAndCartId_ShouldReturnNoCart()
    {
        // Arrange
        ClearAuthenticationHeader();

        // Act
        var response = await GetApiResponseAsync<CartDto>("v1/cart");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().BeNull();
        response.Message.Should().Be("No cart found");
    }

    [Fact]
    public async Task GetCart_WithAuthenticatedUser_ShouldNotAccessOtherUsersCart()
    {
        // Arrange - Create cart for customer user
        var customerToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var customerCartId = addItemResponse!.Data.CartId;

        // Authenticate as different user (admin)
        var adminToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminToken);

        // Act - Try to access customer's cart
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={customerCartId}");

        // Assert - Should return null (access denied)
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().BeNull();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GetCart_WithNonExistentCartId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentCartId = Guid.NewGuid();

        // Act
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={nonExistentCartId}");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().BeNull();
        response.Message.Should().Be("No cart found");
    }

    [Fact]
    public async Task GetCart_WithEmptyGuidCartId_ShouldReturnNull()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={emptyGuid}");

        // Assert
        response.Should().NotBeNull();
        response!.Succeeded.Should().BeTrue();
        response.Data.Should().BeNull();
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task GetCart_ShouldReturnDataConsistentWithDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 5);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Act
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();

        // Verify API response data matches database data
        await ExecuteDbContextAsync(async context =>
        {
            var dbCart = await context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            dbCart.Should().NotBeNull();

            // Verify cart-level data
            response.Data.Id.Should().Be(dbCart!.Id);
            response.Data.UserId.Should().Be(dbCart.UserId);
            response.Data.Items.Should().HaveCount(dbCart.Items.Count);

            // Verify item-level data
            foreach (var dbItem in dbCart.Items)
            {
                var apiItem = response.Data.Items.First(i => i.Id == dbItem.Id);
                apiItem.ProductId.Should().Be(dbItem.ProductId);
                apiItem.VariantId.Should().Be(dbItem.VariantId);
                apiItem.Quantity.Should().Be(dbItem.Quantity);
            }
        });
    }

    [Fact]
    public async Task GetCart_WithUpdatedCart_ShouldReturnLatestData()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();

        // Add item to cart
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Get initial cart state
        var initialResponse = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");
        AssertApiSuccess(initialResponse);
        initialResponse!.Data.TotalItems.Should().Be(1);

        // Add another item
        var (product2Id, variant2Id) = await CreateTestProductAsync();
        var addItemRequest2 = CartTestDataV1.Creation.CreateValidRequest(cartId, product2Id, variant2Id, 3);
        await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest2);

        // Act - Get updated cart
        var updatedResponse = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert
        AssertApiSuccess(updatedResponse);
        updatedResponse!.Data.Should().NotBeNull();
        updatedResponse.Data.TotalItems.Should().Be(4); // 1 + 3
        updatedResponse.Data.Items.Should().HaveCount(2);
        updatedResponse.Data.UpdatedAt.Should().BeAfter(initialResponse.Data.UpdatedAt);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetCart_WithInvalidCartIdFormat_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("v1/cart?cartId=invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCart_MultipleTimesWithSameCartId_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 2);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Act - Get cart multiple times
        var response1 = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");
        var response2 = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");
        var response3 = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert - All responses should be consistent
        AssertApiSuccess(response1);
        AssertApiSuccess(response2);
        AssertApiSuccess(response3);

        response1!.Data.Id.Should().Be(response2!.Data.Id).And.Be(response3!.Data.Id);
        response1.Data.TotalItems.Should().Be(response2.Data.TotalItems).And.Be(response3.Data.TotalItems);
        response1.Data.TotalAmount.Should().Be(response2.Data.TotalAmount).And.Be(response3.Data.TotalAmount);
        response1.Data.Items.Should().HaveCount(response2.Data.Items.Count);
    }

    [Fact]
    public async Task GetCart_WithCartContainingComplexMetadata_ShouldReturnCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Act
        var response = await GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Metadata.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetCart_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var (productId, variantId) = await CreateTestProductAsync();
        var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
        var addItemResponse = await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        AssertApiSuccess(addItemResponse);

        var cartId = addItemResponse!.Data.CartId;

        // Act - Make concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => GetApiResponseAsync<CartDto>($"v1/cart?cartId={cartId}"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed with consistent data
        responses.Should().AllSatisfy(response => AssertApiSuccess(response));

        var firstResponseData = responses[0]!.Data;
        responses.Should().AllSatisfy(response =>
        {
            response!.Data.Id.Should().Be(firstResponseData.Id);
            response.Data.TotalItems.Should().Be(firstResponseData.TotalItems);
            response.Data.TotalAmount.Should().Be(firstResponseData.TotalAmount);
        });
    }

    [Fact]
    public async Task GetCart_WithLargeNumberOfItems_ShouldPerformWell()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create many products and add to cart
        var products = new List<(Guid ProductId, Guid? VariantId)>();
        for (int i = 0; i < 10; i++)
        {
            products.Add(await CreateTestProductAsync());
        }

        foreach (var (productId, variantId) in products)
        {
            var addItemRequest = CartTestDataV1.Creation.CreateValidRequest(null, productId, variantId, 1);
            await PostApiResponseAsync<object, AddItemToCartResponseV1>("v1/cart/items", addItemRequest);
        }

        // Act & Assert - Measure response time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await GetApiResponseAsync<CartDto>("v1/cart");
        stopwatch.Stop();

        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Items.Should().HaveCount(10);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    #endregion

    #region HTTP Status Code Tests

    [Fact]
    public async Task GetCart_WithValidRequest_ShouldReturnStatus200()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<CartDto>("v1/cart");

        // Assert
        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(200);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test product with a variant and returns the IDs
    /// </summary>
    private async Task<(Guid ProductId, Guid? VariantId)> CreateTestProductAsync()
    {
        // Ensure admin authentication for product creation
        var adminToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminToken);

        // Create a category first
        var categoryId = await CreateTestCategoryAsync();

        // Create product
        var productRequest = ProductTestDataV1.Creation.CreateValidRequest(
            name: $"Test Product {Guid.NewGuid():N}",
            slug: $"test-product-{Guid.NewGuid():N}",
            categoryIds: new List<Guid> { categoryId });

        var productResponse = await PostMultipartApiResponseAsync<CreateProductResponseV1>("v1/products", productRequest);
        AssertApiSuccess(productResponse);

        var variantId = await ExecuteDbContextAsync(async context =>
        {
            var variant = await context.ProductVariants
                .FirstOrDefaultAsync(v => v.ProductId == productResponse!.Data.Id);

            return variant?.Id;
        });

        // Restore customer authentication if needed
        var customerToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerToken);

        return (productResponse!.Data.Id, variantId);
    }

    /// <summary>
    /// Creates a test category and returns the ID
    /// </summary>
    private async Task<Guid> CreateTestCategoryAsync()
    {
        var categoryRequest = CategoryTestDataV1.Creation.CreateValidRequest(
            name: $"Test Category {Guid.NewGuid():N}",
            slug: $"test-category-{Guid.NewGuid():N}");

        var categoryResponse = await PostApiResponseAsync<object, CreateCategoryResponseV1>("v1/categories", categoryRequest);
        AssertApiSuccess(categoryResponse);

        return categoryResponse!.Data.Id;
    }

    #endregion
}
