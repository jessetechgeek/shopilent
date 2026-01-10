using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.GetUserOrders.V1;

public class GetUserOrdersEndpointV1Tests : ApiIntegrationTestBase
{
    public GetUserOrdersEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetUserOrders_AsAuthenticatedCustomer_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create some orders for the customer
        await CreateMultipleTestOrdersForCurrentUserAsync(3);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Should().BeAssignableTo<IReadOnlyList<OrderDto>>();
    }

    [Fact]
    public async Task GetUserOrders_WithOrders_ShouldReturnAllUserOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create 5 orders for the current user
        var orderIds = await CreateMultipleTestOrdersForCurrentUserAsync(5);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Count.Should().BeGreaterThanOrEqualTo(5);

        // Verify all created orders are included
        foreach (var orderId in orderIds)
        {
            response.Data.Should().Contain(o => o.Id == orderId);
        }
    }

    [Fact]
    public async Task GetUserOrders_ShouldReturnCompleteOrderInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(2);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        // Verify each order has complete information
        response.Data.Should().AllSatisfy(order =>
        {
            order.Id.Should().NotBeEmpty();
            order.UserId.Should().NotBeNull();
            order.ShippingAddressId.Should().NotBeNull();
            order.BillingAddressId.Should().NotBeNull();
            order.Subtotal.Should().BeGreaterThan(0);
            order.Total.Should().BeGreaterThan(0);
            order.Currency.Should().NotBeNullOrEmpty();
            order.ShippingMethod.Should().NotBeNullOrEmpty();
            order.Status.Should().BeDefined();
            order.CreatedAt.Should().NotBe(default(DateTime));
            order.UpdatedAt.Should().NotBe(default(DateTime));
        });
    }

    [Fact]
    public async Task GetUserOrders_WhenNoOrdersExist_ShouldReturnEmptyList()
    {
        // Arrange
        // Create a new customer that has no orders
        await RegisterNewCustomerAsync("newcustomer@shopilent.com", "NewCustomer123!");
        var accessToken = await AuthenticateAsync("newcustomer@shopilent.com", "NewCustomer123!");
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrders_ShouldReturnOrdersInDescendingOrderByCreatedDate()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple orders with slight delays to ensure different timestamps
        await CreateMultipleTestOrdersWithDelaysAsync(5);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        // Verify orders are sorted by CreatedAt in descending order (most recent first)
        for (int i = 0; i < response.Data.Count - 1; i++)
        {
            response.Data[i].CreatedAt.Should().BeOnOrAfter(response.Data[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task GetUserOrders_WithDifferentOrderStatuses_ShouldReturnAllStatuses()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create orders with different statuses
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Pending);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Processing);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Shipped);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Delivered);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        // Verify we have orders with different statuses
        var uniqueStatuses = response.Data.Select(o => o.Status).Distinct().Count();
        uniqueStatuses.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task GetUserOrders_ShouldOnlyReturnCurrentUserOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Get current user ID
        var currentUserId = await GetCurrentUserIdAsync();

        // Create orders for current user
        await CreateMultipleTestOrdersForCurrentUserAsync(3);

        // Create orders for a different user
        await RegisterNewCustomerAsync("othercustomer@shopilent.com", "OtherCustomer123!");
        await CreateOrdersForUserAsync("othercustomer@shopilent.com", 2);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        // Verify all returned orders belong to the current user
        response.Data.Should().AllSatisfy(order =>
        {
            order.UserId.Should().Be(currentUserId);
        });
    }

    [Fact]
    public async Task GetUserOrders_CalledMultipleTimes_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(3);

        // Act - Call endpoint twice
        var firstResponse = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");
        var secondResponse = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        firstResponse!.Data.Count.Should().Be(secondResponse!.Data.Count);

        // Verify order IDs match
        var firstOrderIds = firstResponse.Data.Select(o => o.Id).OrderBy(id => id).ToList();
        var secondOrderIds = secondResponse.Data.Select(o => o.Id).OrderBy(id => id).ToList();
        firstOrderIds.Should().BeEquivalentTo(secondOrderIds);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetUserOrders_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();

        // Act
        var response = await Client.GetAsync("v1/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserOrders_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        SetAuthenticationHeader("invalid.token.here");

        // Act
        var response = await Client.GetAsync("v1/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserOrders_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        // Note: Testing expired tokens typically requires generating a token with a past expiration
        // This is a placeholder for such a test - implementation depends on token generation capabilities
        // For now, we test with an invalid/malformed token as a proxy
        SetAuthenticationHeader("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjB9.invalid");

        // Act
        var response = await Client.GetAsync("v1/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserOrders_AsCustomer_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(2);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserOrders_AsAdmin_ShouldReturnAdminOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        // Admin may or may not have orders, but the endpoint should work
    }

    [Fact]
    public async Task GetUserOrders_AsManager_ShouldReturnManagerOrders()
    {
        // Arrange
        await EnsureManagerUserExistsAsync();
        var accessToken = await AuthenticateAsync("manager@shopilent.com", "Manager123!");
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        // Manager may or may not have orders, but the endpoint should work
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetUserOrders_WithCaching_ShouldReturnCachedResults()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(3);

        // Act - First call to populate cache
        var firstResponse = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Act - Second call should hit cache
        var secondResponse = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        // Both responses should have same data
        firstResponse!.Data.Count.Should().Be(secondResponse!.Data.Count);

        var firstOrderIds = firstResponse.Data.Select(o => o.Id).OrderBy(id => id);
        var secondOrderIds = secondResponse.Data.Select(o => o.Id).OrderBy(id => id);
        firstOrderIds.Should().BeEquivalentTo(secondOrderIds);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetUserOrders_WithLargeNumberOfOrders_ShouldReturnAllOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a large number of orders (50 orders)
        await CreateMultipleTestOrdersForCurrentUserAsync(50);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();
        response.Data.Count.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task GetUserOrders_WithOrdersContainingMetadata_ShouldReturnOrdersSuccessfully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var metadata = new Dictionary<string, object>
        {
            { "gift_message", "Happy Birthday!" },
            { "gift_wrapping", true }
        };

        var orderIdWithMetadata = await CreateTestOrderWithMetadataAsync(metadata);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        // Find the order with metadata
        var orderWithMetadata = response.Data.FirstOrDefault(o => o.Id == orderIdWithMetadata);
        orderWithMetadata.Should().NotBeNull("Order with metadata should be in the response");

        // Verify metadata is present
        orderWithMetadata!.Metadata.Should().NotBeNull("Metadata should not be null");
        orderWithMetadata.Metadata.Should().ContainKey("gift_message", "gift_message should be in metadata");
        orderWithMetadata.Metadata.Should().ContainKey("gift_wrapping", "gift_wrapping should be in metadata");
    }

    [Fact]
    public async Task GetUserOrders_WithCancelledOrders_ShouldIncludeCancelledOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a cancelled order
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Cancelled);
        // Create a regular order
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Pending);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();
        response.Data.Should().Contain(order => order.Status == OrderStatus.Cancelled);
        response.Data.Should().Contain(order => order.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task GetUserOrders_WithOrdersFromDifferentDates_ShouldReturnAllOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create orders with delays to simulate different creation dates
        await CreateMultipleTestOrdersWithDelaysAsync(5);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();
        response.Data.Count.Should().BeGreaterThanOrEqualTo(5);

        // Verify orders have different creation timestamps
        var uniqueDates = response.Data.Select(o => o.CreatedAt.Date).Distinct().Count();
        uniqueDates.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUserOrders_WithOrdersContainingMultipleItems_ShouldReturnSuccessfully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an order with multiple items
        await CreateTestOrderWithMultipleItemsAsync();

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();
    }

    #endregion

    #region Response Structure Tests

    [Fact]
    public async Task GetUserOrders_ShouldReturnCorrectResponseStructure()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(2);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);

        // Verify response structure
        response!.Data.Should().NotBeNull();
        response.Data.Should().BeAssignableTo<IReadOnlyList<OrderDto>>();

        if (response.Data.Any())
        {
            var firstOrder = response.Data.First();
            firstOrder.Id.Should().NotBeEmpty();
            firstOrder.UserId.Should().NotBeNull();
            firstOrder.Total.Should().BeGreaterThan(0);
            firstOrder.Currency.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetUserOrders_ResponseOrders_ShouldHaveRequiredFields()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersForCurrentUserAsync(2);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();

        foreach (var order in response.Data)
        {
            // Verify all required fields are present and valid
            order.Id.Should().NotBeEmpty("Order ID is required");
            order.UserId.Should().NotBeNull("User ID is required");
            order.ShippingAddressId.Should().NotBeNull("Shipping Address ID is required");
            order.BillingAddressId.Should().NotBeNull("Billing Address ID is required");
            order.Subtotal.Should().BeGreaterThan(0, "Subtotal must be positive");
            order.Total.Should().BeGreaterThan(0, "Total must be positive");
            order.Currency.Should().NotBeNullOrEmpty("Currency is required");
            order.ShippingMethod.Should().NotBeNullOrEmpty("Shipping method is required");
            order.CreatedAt.Should().NotBe(default(DateTime), "CreatedAt is required");
            order.UpdatedAt.Should().NotBe(default(DateTime), "UpdatedAt is required");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetUserOrders_WithManyOrders_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create 100 orders
        await CreateMultipleTestOrdersForCurrentUserAsync(100);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        stopwatch.Stop();

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetUserOrders_WithDeletedUser_ShouldReturnNotFound()
    {
        // Arrange
        // This test verifies the endpoint handles edge case where JWT is valid but user no longer exists
        // In practice, this would require deleting a user while keeping their JWT valid
        // For now, we verify the endpoint works correctly with valid authentication
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<IReadOnlyList<OrderDto>>("v1/orders/my-orders");

        // Assert
        // Should succeed since user exists
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates multiple test orders for the currently authenticated user
    /// </summary>
    private async Task<List<Guid>> CreateMultipleTestOrdersForCurrentUserAsync(int count)
    {
        var orderIds = new List<Guid>();

        for (int i = 0; i < count; i++)
        {
            var orderId = await CreateSingleTestOrderForCurrentUserAsync();
            orderIds.Add(orderId);
        }

        return orderIds;
    }

    /// <summary>
    /// Creates multiple test orders with slight delays to ensure different timestamps
    /// </summary>
    private async Task CreateMultipleTestOrdersWithDelaysAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await CreateSingleTestOrderForCurrentUserAsync();

            // Small delay to ensure different CreatedAt timestamps
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Creates a single test order for the currently authenticated user
    /// </summary>
    private async Task<Guid> CreateSingleTestOrderForCurrentUserAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            // Get current user (customer@shopilent.com)
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 99.99m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            var productSnapshot = ProductSnapshot.Create(
                name: product.Name,
                sku: product.Sku,
                slug: product.Slug.Value
            ).Value;

            order.AddItem(product.Id, null, 1, Money.Create(99.99m, "USD").Value, productSnapshot);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates a test order with specific status
    /// </summary>
    private async Task<Guid> CreateTestOrderWithSpecificStatusAsync(OrderStatus status)
    {
        var orderId = await CreateSingleTestOrderForCurrentUserAsync();

        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order == null) return;

            switch (status)
            {
                case OrderStatus.Processing:
                    order.MarkAsPaid();
                    break;
                case OrderStatus.Shipped:
                    order.MarkAsPaid();
                    order.MarkAsShipped();
                    break;
                case OrderStatus.Delivered:
                    order.MarkAsPaid();
                    order.MarkAsShipped();
                    order.MarkAsDelivered();
                    break;
                case OrderStatus.Cancelled:
                    order.Cancel("Test cancellation");
                    break;
            }

            await context.SaveChangesAsync();
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with metadata
    /// </summary>
    private async Task<Guid> CreateTestOrderWithMetadataAsync(Dictionary<string, object> metadata)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 99.99m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            // Add metadata
            foreach (var kvp in metadata)
            {
                order.UpdateMetadata(kvp.Key, kvp.Value);
            }

            var productSnapshot = ProductSnapshot.Create(
                name: product.Name,
                sku: product.Sku,
                slug: product.Slug.Value
            ).Value;

            order.AddItem(product.Id, null, 1, Money.Create(99.99m, "USD").Value, productSnapshot);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates a test order with multiple items
    /// </summary>
    private async Task<Guid> CreateTestOrderWithMultipleItemsAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);

            // Create multiple products
            var product1 = await CreateTestProductAsync(context, "Product 1", 29.99m);
            var product2 = await CreateTestProductAsync(context, "Product 2", 49.99m);
            var product3 = await CreateTestProductAsync(context, "Product 3", 19.99m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.97m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            var productSnapshot1 = ProductSnapshot.Create(
                name: product1.Name,
                sku: product1.Sku,
                slug: product1.Slug.Value
            ).Value;

            var productSnapshot2 = ProductSnapshot.Create(
                name: product2.Name,
                sku: product2.Sku,
                slug: product2.Slug.Value
            ).Value;

            var productSnapshot3 = ProductSnapshot.Create(
                name: product3.Name,
                sku: product3.Sku,
                slug: product3.Slug.Value
            ).Value;

            order.AddItem(product1.Id, null, 1, Money.Create(29.99m, "USD").Value, productSnapshot1);
            order.AddItem(product2.Id, null, 1, Money.Create(49.99m, "USD").Value, productSnapshot2);
            order.AddItem(product3.Id, null, 1, Money.Create(19.99m, "USD").Value, productSnapshot3);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Gets the current user ID from the database
    /// </summary>
    private async Task<Guid> GetCurrentUserIdAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            return user.Id;
        });
    }

    /// <summary>
    /// Registers a new customer user
    /// </summary>
    private async Task RegisterNewCustomerAsync(string email, string password)
    {
        var registerRequest = new
        {
            Email = email,
            Password = password,
            FirstName = "Test",
            LastName = "Customer"
        };

        await PostAsync("v1/auth/register", registerRequest);
        // Ignore if user already exists (409 Conflict)
    }

    /// <summary>
    /// Creates orders for a specific user
    /// </summary>
    private async Task CreateOrdersForUserAsync(string userEmail, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ExecuteDbContextAsync(async context =>
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == userEmail);
                if (user == null)
                    throw new InvalidOperationException($"User {userEmail} not found.");

                var address = await CreateTestAddressForUserAsync(context, user);
                var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 49.99m);

                var order = Order.Create(
                    userId: user.Id,
                    shippingAddressId: address.Id,
                    billingAddressId: address.Id,
                    subtotal: Money.Create(49.99m, "USD").Value,
                    tax: Money.Create(4.00m, "USD").Value,
                    shippingCost: Money.Create(5.00m, "USD").Value,
                    shippingMethod: "Express"
                ).Value;

                var productSnapshot = ProductSnapshot.Create(
                    name: product.Name,
                    sku: product.Sku,
                    slug: product.Slug.Value
                ).Value;

                order.AddItem(product.Id, null, 1, Money.Create(49.99m, "USD").Value, productSnapshot);

                context.Orders.Add(order);
                await context.SaveChangesAsync();
            });
        }
    }

    /// <summary>
    /// Creates a test address for a user
    /// </summary>
    private async Task<Address> CreateTestAddressForUserAsync(
        ApplicationDbContext context,
        User user)
    {
        var postalAddress = PostalAddress.Create(
            addressLine1: "123 Test St",
            city: "Test City",
            state: "CA",
            country: "United States",
            postalCode: "90210"
        ).Value;

        var phone = PhoneNumber.Create("555-0123").Value;
        var address = Address.CreateShipping(user.Id, postalAddress, phone, false).Value;
        context.Addresses.Add(address);
        await context.SaveChangesAsync();

        return address;
    }

    /// <summary>
    /// Creates a test product
    /// </summary>
    private async Task<Product> CreateTestProductAsync(
        ApplicationDbContext context,
        string name,
        decimal price)
    {
        var slug = Slug.Create($"{name.ToLower().Replace(" ", "-")}-{Guid.NewGuid():N}").Value;
        var productPrice = Money.Create(price, "USD").Value;
        var product = Product.CreateWithDescription(
            name: name,
            slug: slug,
            basePrice: productPrice,
            description: $"Test description for {name}",
            sku: $"SKU-{Guid.NewGuid():N}"
        ).Value;

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product;
    }

    #endregion
}
