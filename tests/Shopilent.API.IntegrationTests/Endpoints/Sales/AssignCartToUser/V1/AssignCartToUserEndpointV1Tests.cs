using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.AssignCartToUser.V1;

public class AssignCartToUserEndpointV1Tests : ApiIntegrationTestBase
{
    public AssignCartToUserEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task AssignCartToUser_WithValidAnonymousCart_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart (no user assigned)
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().Be("Cart successfully assigned to user");
        response.Message.Should().Be("Cart assigned to user");
    }

    [Fact]
    public async Task AssignCartToUser_WithValidCart_ShouldAssignCartInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);

        // Verify cart is now assigned to the authenticated user
        await ExecuteDbContextAsync(async context =>
        {
            var updatedCart = await context.Carts
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            updatedCart.Should().NotBeNull();
            updatedCart!.UserId.Should().NotBeNull();
            updatedCart.UserId.Should().NotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task AssignCartToUser_WhenCartAlreadyAssignedToSameUser_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a cart and assign it to the current user
        var cart = await CreateCartForCurrentUserAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().Be("Cart successfully assigned to user");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task AssignCartToUser_WithEmptyCartId_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var request = new { CartId = Guid.Empty };

        // Act
        var response = await PostAsync("v1/cart/assign", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Cart ID is required.");
    }

    [Fact]
    public async Task AssignCartToUser_WithNonExistentCartId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var request = new
        {
            CartId = Guid.NewGuid() // Non-existent cart
        };

        // Act
        var response = await PostAsync("v1/cart/assign", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "Cart not found");
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task AssignCartToUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostAsync("v1/cart/assign", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignCartToUser_WithAuthenticatedUser_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task AssignCartToUser_WithAdminUser_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task AssignCartToUser_WhenCartAlreadyAssignedToDifferentUser_ShouldReturnValidationError()
    {
        // Arrange - Create first user (customer) and assign cart
        var firstUserToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(firstUserToken);
        var cart = await CreateAnonymousCartAsync();

        var assignRequest = new { CartId = cart.Id };
        await PostApiResponseAsync<object, string>("v1/cart/assign", assignRequest);

        // Arrange - Switch to second user (admin)
        var secondUserToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(secondUserToken);

        // Act - Try to assign the same cart to second user (admin)
        var response = await PostAsync("v1/cart/assign", assignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("already assigned", "another user");
    }

    [Fact]
    public async Task AssignCartToUser_WhenUserAlreadyHasCart_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create and assign first cart to user
        var existingCart = await CreateAnonymousCartAsync();
        var firstAssignRequest = new { CartId = existingCart.Id };
        await PostApiResponseAsync<object, string>("v1/cart/assign", firstAssignRequest);

        // Create a second anonymous cart
        var newCart = await CreateAnonymousCartAsync();
        var secondAssignRequest = new { CartId = newCart.Id };

        // Act - Try to assign second cart to same user
        var response = await PostAsync("v1/cart/assign", secondAssignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("already has", "assigned cart", "existing cart");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task AssignCartToUser_WithCartContainingItems_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart with items
        var cart = await CreateAnonymousCartWithItemsAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);

        // Verify cart items are preserved
        await ExecuteDbContextAsync(async context =>
        {
            var updatedCart = await context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            updatedCart.Should().NotBeNull();
            updatedCart!.Items.Should().NotBeEmpty();
            updatedCart.UserId.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task AssignCartToUser_WithEmptyCart_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an empty anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act
        var response = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert
        AssertApiSuccess(response);

        // Verify cart is assigned
        await ExecuteDbContextAsync(async context =>
        {
            var updatedCart = await context.Carts
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            updatedCart.Should().NotBeNull();
            updatedCart!.UserId.Should().NotBeNull();
        });
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task AssignCartToUser_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act - Call assign endpoint multiple times
        var firstResponse = await PostApiResponseAsync<object, string>("v1/cart/assign", request);
        var secondResponse = await PostApiResponseAsync<object, string>("v1/cart/assign", request);
        var thirdResponse = await PostApiResponseAsync<object, string>("v1/cart/assign", request);

        // Assert - All should succeed
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);
        AssertApiSuccess(thirdResponse);

        // Verify cart is still assigned correctly
        await ExecuteDbContextAsync(async context =>
        {
            var updatedCart = await context.Carts
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            updatedCart.Should().NotBeNull();
            updatedCart!.UserId.Should().NotBeNull();
        });
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task AssignCartToUser_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an anonymous cart
        var cart = await CreateAnonymousCartAsync();

        var request = new { CartId = cart.Id };

        // Act - Make concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => PostApiResponseAsync<object, string>("v1/cart/assign", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - At least one should succeed, others may have concurrency conflicts
        var successfulResponses = responses.Where(r => r != null && r.Succeeded).ToList();
        var failedResponses = responses.Where(r => r != null && !r.Succeeded).ToList();

        // At least one request should succeed
        successfulResponses.Should().NotBeEmpty("at least one concurrent request should succeed");

        // Failed responses should be due to concurrency conflicts (if any)
        if (failedResponses.Any())
        {
            failedResponses.Should().AllSatisfy(response =>
            {
                response.Errors.Should().Contain(e =>
                    e.Contains("Concurrency conflict") ||
                    e.Contains("already assigned") ||
                    e.Contains("already has"));
            });
        }

        // Verify cart is assigned to the user
        await ExecuteDbContextAsync(async context =>
        {
            var updatedCart = await context.Carts
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            updatedCart.Should().NotBeNull();
            updatedCart!.UserId.Should().NotBeNull();
        });
    }

    #endregion

    #region Boundary Tests

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Empty GUID
    public async Task AssignCartToUser_WithInvalidCartIdFormat_ShouldReturnValidationError(string cartIdString)
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var request = new { CartId = Guid.Parse(cartIdString) };

        // Act
        var response = await PostAsync("v1/cart/assign", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates an anonymous cart (not assigned to any user)
    /// </summary>
    private async Task<Cart> CreateAnonymousCartAsync()
    {
        var cart = Cart.Create(null).Value;

        await ExecuteDbContextAsync(async context =>
        {
            await context.Carts.AddAsync(cart);
            await context.SaveChangesAsync();
        });

        return cart;
    }

    /// <summary>
    /// Creates an anonymous cart with items
    /// </summary>
    private async Task<Cart> CreateAnonymousCartWithItemsAsync()
    {
        Cart? createdCart = null;

        await ExecuteDbContextAsync(async context =>
        {
            // Create a simple product with variant using domain entities directly
            var productResult = Product.CreateWithDescription(
                name: "Test Product",
                slug: Slug.Create($"test-product-{Guid.NewGuid():N}").Value,
                basePrice: Money.Create(100m, "USD").Value,
                description: "Test Description",
                sku: $"SKU-{Guid.NewGuid():N}"
            );

            var product = productResult.Value;

            // Create a variant for the product
            var variantResult = ProductVariant.Create(
                productId: product.Id,
                sku: $"VAR-{Guid.NewGuid():N}",
                price: Money.Create(100m, "USD").Value,
                stockQuantity: 10
            );

            var variant = variantResult.Value;

            // Add variant to product
            product.AddVariant(variant);

            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();

            // Create cart
            var cart = Cart.Create(null).Value;

            // Add item to cart
            cart.AddItem(product.Id, 1, variant?.Id);

            await context.Carts.AddAsync(cart);
            await context.SaveChangesAsync();

            createdCart = cart;
        });

        return createdCart!;
    }

    /// <summary>
    /// Creates a cart assigned to the current authenticated user
    /// </summary>
    private async Task<Cart> CreateCartForCurrentUserAsync()
    {
        Cart? createdCart = null;

        await ExecuteDbContextAsync(async context =>
        {
            // Get the authenticated customer user (created in test fixture)
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");

            if (user == null)
            {
                // If customer doesn't exist, get any user or create one
                user = await context.Users.FirstOrDefaultAsync();

                if (user == null)
                {
                    throw new InvalidOperationException("No users found in database. Test fixture may not be set up correctly.");
                }
            }

            // Create cart for user
            var cart = Cart.Create(user.Id).Value;

            await context.Carts.AddAsync(cart);
            await context.SaveChangesAsync();

            createdCart = cart;
        });

        return createdCart!;
    }

    /// <summary>
    /// Registers and verifies a new user
    /// </summary>
    private async Task RegisterAndVerifyUserAsync(string email, string password)
    {
        var registerRequest = new
        {
            Email = email,
            Password = password,
            FirstName = "Test",
            LastName = "User",
            Phone = "+1234567890"
        };

        var registerResponse = await PostAsync("v1/register", registerRequest);

        // If registration fails, try alternate endpoint or skip if user exists
        if (!registerResponse.IsSuccessStatusCode)
        {
            // User might already exist, just verify them in the database
            await ExecuteDbContextAsync(async context =>
            {
                var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == email);
                if (existingUser != null && !existingUser.EmailVerified)
                {
                    existingUser.VerifyEmail();
                    await context.SaveChangesAsync();
                }
            });
            return;
        }

        // Verify email by directly updating database
        await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == email);
            if (user != null)
            {
                user.VerifyEmail();
                await context.SaveChangesAsync();
            }
        });
    }

    #endregion
}
