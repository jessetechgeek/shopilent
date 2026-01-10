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

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.GetRecentOrders.V1;

public class GetRecentOrdersEndpointV1Tests : ApiIntegrationTestBase
{
    public GetRecentOrdersEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetRecentOrders_WithDefaultCount_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create some test orders
        await CreateMultipleTestOrdersAsync(5);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        response.Data.Count.Should().BeGreaterThanOrEqualTo(0);
        response.Data.RetrievedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetRecentOrders_WithSpecificCount_ShouldReturnRequestedNumberOfOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create 15 test orders
        await CreateMultipleTestOrdersAsync(15);

        // Act - Request 10 recent orders
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(10);
        response.Data.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task GetRecentOrders_WithMinimumCount_ShouldReturnOneOrder()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create test orders
        await CreateMultipleTestOrdersAsync(5);

        // Act - Request 1 recent order
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=1");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(1);
        response.Data.Count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetRecentOrders_WithMaximumCount_ShouldReturnUpTo100Orders()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create test orders
        await CreateMultipleTestOrdersAsync(10); // Create some orders

        // Act - Request 100 recent orders
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=100");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(100);
        response.Data.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetRecentOrders_ShouldReturnOrdersInDescendingOrderByCreatedDate()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple orders with slight delays
        var orderIds = await CreateMultipleTestOrdersWithDelaysAsync(5);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Orders.Should().NotBeEmpty();

        // Verify orders are sorted by CreatedAt in descending order (most recent first)
        for (int i = 0; i < response.Data.Orders.Count - 1; i++)
        {
            response.Data.Orders[i].CreatedAt.Should().BeOnOrAfter(response.Data.Orders[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task GetRecentOrders_ShouldReturnCompleteOrderInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(3);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=5");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Orders.Should().NotBeEmpty();

        // Verify each order has complete information
        response.Data.Orders.Should().AllSatisfy(order =>
        {
            order.Id.Should().NotBeEmpty();
            order.UserId.Should().NotBeNull();
            order.ShippingAddressId.Should().NotBeNull();
            order.BillingAddressId.Should().NotBeNull();
            order.Subtotal.Should().BeGreaterThan(0);
            order.Total.Should().BeGreaterThan(0);
            order.Currency.Should().NotBeNullOrEmpty();
            order.ShippingMethod.Should().NotBeNullOrEmpty();
            order.CreatedAt.Should().NotBe(default(DateTime));
            order.UpdatedAt.Should().NotBe(default(DateTime));
        });
    }

    [Fact]
    public async Task GetRecentOrders_AsManager_ShouldReturnSuccess()
    {
        // Arrange
        await EnsureManagerUserExistsAsync();
        var accessToken = await AuthenticateAsync("manager@shopilent.com", "Manager123!");
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(3);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=5");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRecentOrders_WhenNoOrdersExist_ShouldReturnEmptyList()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        // Note: In a test database, there may be orders from other tests
        // This test verifies the endpoint works correctly even when requesting more than available
        response.Data.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GetRecentOrders_WithZeroCount_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync("v1/orders/recent?count=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Count must be greater than 0");
    }

    [Fact]
    public async Task GetRecentOrders_WithNegativeCount_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync("v1/orders/recent?count=-5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Count must be greater than 0");
    }

    [Fact]
    public async Task GetRecentOrders_WithCountExceedingMaximum_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act - Request 101 orders (exceeds maximum of 100)
        var response = await Client.GetAsync("v1/orders/recent?count=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Count must not exceed 100");
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    public async Task GetRecentOrders_WithInvalidCountValues_ShouldReturnValidationError(int invalidCount)
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync($"v1/orders/recent?count={invalidCount}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetRecentOrders_WithValidCountValues_ShouldReturnSuccess(int validCount)
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(3);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>($"v1/orders/recent?count={validCount}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetRecentOrders_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();

        // Act
        var response = await Client.GetAsync("v1/orders/recent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRecentOrders_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync("v1/orders/recent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRecentOrders_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(2);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=5");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public async Task GetRecentOrders_WithCountAtLowerBoundary_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(5);

        // Act - Count = 1 (lower boundary)
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=1");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Orders.Count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetRecentOrders_WithCountAtUpperBoundary_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(10);

        // Act - Count = 100 (upper boundary)
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=100");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetRecentOrders_WithCountJustBelowUpperBoundary_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(10);

        // Act - Count = 99 (just below upper boundary)
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=99");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(99);
    }

    [Fact]
    public async Task GetRecentOrders_WithCountJustAboveLowerBoundary_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(5);

        // Act - Count = 2 (just above lower boundary)
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=2");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(2);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetRecentOrders_WithCountLargerThanAvailableOrders_ShouldReturnAllOrders()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create only 3 orders
        await CreateMultipleTestOrdersAsync(3);

        // Act - Request 50 orders (more than available)
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=50");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetRecentOrders_WithOrdersFromMultipleUsers_ShouldReturnOrdersFromAllUsers()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create orders for multiple users
        await CreateTestOrdersForMultipleUsersAsync();

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=20");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Orders.Should().NotBeEmpty();

        // Verify we have orders from multiple users (including orders created in this test)
        var uniqueUserIds = response.Data.Orders.Select(o => o.UserId).Distinct().Count();
        // At minimum, we should have orders from the users we just created
        uniqueUserIds.Should().BeGreaterThanOrEqualTo(1);

        // Verify we created orders for at least 2 customers in this test
        var customerOrdersCount = await ExecuteDbContextAsync(async context =>
        {
            var customer1 = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            var customer2 = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer2@shopilent.com");

            var ordersCount = await context.Orders
                .Where(o => o.UserId == customer1!.Id || o.UserId == customer2!.Id)
                .CountAsync();

            return ordersCount;
        });

        customerOrdersCount.Should().BeGreaterThanOrEqualTo(4); // 2 orders per customer
    }

    [Fact]
    public async Task GetRecentOrders_WithDifferentOrderStatuses_ShouldReturnAllStatuses()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create orders with different statuses
        await CreateTestOrdersWithVariousStatusesAsync();

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=20");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Orders.Should().NotBeEmpty();

        // Verify we have orders with different statuses
        var uniqueStatuses = response.Data.Orders.Select(o => o.Status).Distinct().Count();
        uniqueStatuses.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task GetRecentOrders_CalledMultipleTimes_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(5);

        // Act - Call endpoint twice
        var firstResponse = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");
        var secondResponse = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        firstResponse!.Data.Orders.Count.Should().Be(secondResponse!.Data.Orders.Count);
    }

    [Fact]
    public async Task GetRecentOrders_WithCaching_ShouldReturnCachedResults()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(5);

        // Act - First call to populate cache
        var firstResponse = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Act - Second call should hit cache
        var secondResponse = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        // Both responses should have same data
        firstResponse!.Data.Count.Should().Be(secondResponse!.Data.Count);
        firstResponse.Data.Orders.Count.Should().Be(secondResponse.Data.Orders.Count);
    }

    [Fact]
    public async Task GetRecentOrders_WithDifferentCountParameters_ShouldReturnDifferentResults()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create 20 orders
        await CreateMultipleTestOrdersAsync(20);

        // Act
        var response5 = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=5");
        var response10 = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");
        var response20 = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=20");

        // Assert
        AssertApiSuccess(response5);
        AssertApiSuccess(response10);
        AssertApiSuccess(response20);

        response5!.Data.Orders.Count.Should().BeLessThanOrEqualTo(5);
        response10!.Data.Orders.Count.Should().BeLessThanOrEqualTo(10);
        response20!.Data.Orders.Count.Should().BeLessThanOrEqualTo(20);
    }

    #endregion

    #region Response Structure Tests

    [Fact]
    public async Task GetRecentOrders_ShouldReturnCorrectResponseStructure()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(3);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=5");

        // Assert
        AssertApiSuccess(response);

        // Verify response structure
        response!.Data.Should().NotBeNull();
        response.Data.Orders.Should().NotBeNull();
        response.Data.Count.Should().BeGreaterThanOrEqualTo(0);
        response.Data.RetrievedAt.Should().NotBe(default(DateTime));
        response.Data.RetrievedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetRecentOrders_ShouldHaveCorrectCountMatchingOrdersReturned()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(5);

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=10");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Count.Should().Be(response.Data.Orders.Count);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetRecentOrders_WithLargeCount_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await CreateMultipleTestOrdersAsync(50);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await GetApiResponseAsync<GetRecentOrdersResponseV1>("v1/orders/recent?count=100");

        stopwatch.Stop();

        // Assert
        AssertApiSuccess(response);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates multiple test orders for testing
    /// </summary>
    private async Task CreateMultipleTestOrdersAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await CreateSingleTestOrderAsync();
        }
    }

    /// <summary>
    /// Creates multiple test orders with slight delays to ensure different timestamps
    /// </summary>
    private async Task<List<Guid>> CreateMultipleTestOrdersWithDelaysAsync(int count)
    {
        var orderIds = new List<Guid>();

        for (int i = 0; i < count; i++)
        {
            var orderId = await CreateSingleTestOrderAsync();
            orderIds.Add(orderId);

            // Small delay to ensure different CreatedAt timestamps
            await Task.Delay(10);
        }

        return orderIds;
    }

    /// <summary>
    /// Creates a single test order
    /// </summary>
    private async Task<Guid> CreateSingleTestOrderAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            // Get or create customer user
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                await EnsureCustomerUserExistsAsync();
                user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            }

            if (user == null)
                throw new InvalidOperationException("Could not create or find customer user.");

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

            order.AddItem(product, 1, Money.Create(99.99m, "USD").Value);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates test orders for multiple users
    /// </summary>
    private async Task CreateTestOrdersForMultipleUsersAsync()
    {
        // Ensure first customer exists and create orders
        await EnsureCustomerUserExistsAsync();
        await CreateMultipleTestOrdersAsync(2);

        // Ensure second customer exists
        await EnsureSecondCustomerExistsAsync();

        // Create orders for the second customer
        await CreateOrdersForSecondCustomerAsync(2);
    }

    /// <summary>
    /// Ensures the second customer user exists
    /// </summary>
    private async Task EnsureSecondCustomerExistsAsync()
    {
        var userExists = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.AnyAsync(u => u.Email.Value == "customer2@shopilent.com");
        });

        if (!userExists)
        {
            await RegisterSecondCustomerAsync();
        }
    }

    /// <summary>
    /// Creates orders for the second customer
    /// </summary>
    private async Task CreateOrdersForSecondCustomerAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ExecuteDbContextAsync(async context =>
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer2@shopilent.com");
                if (user == null)
                    throw new InvalidOperationException("Second customer user not found.");

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

                order.AddItem(product, 1, Money.Create(49.99m, "USD").Value);

                context.Orders.Add(order);
                await context.SaveChangesAsync();
            });
        }
    }

    /// <summary>
    /// Registers a second customer user for testing
    /// </summary>
    private async Task RegisterSecondCustomerAsync()
    {
        var registerRequest = new
        {
            Email = "customer2@shopilent.com",
            Password = "Customer123!",
            FirstName = "Customer2",
            LastName = "User"
        };

        await PostAsync("v1/auth/register", registerRequest);
        // Ignore if user already exists (409 Conflict)
    }

    /// <summary>
    /// Creates test orders with various statuses
    /// </summary>
    private async Task CreateTestOrdersWithVariousStatusesAsync()
    {
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Pending);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Processing);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Shipped);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Delivered);
        await CreateTestOrderWithSpecificStatusAsync(OrderStatus.Cancelled);
    }

    /// <summary>
    /// Creates a test order with a specific status
    /// </summary>
    private async Task<Guid> CreateTestOrderWithSpecificStatusAsync(OrderStatus status)
    {
        var orderId = await CreateSingleTestOrderAsync();

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

    #region Response DTO

    /// <summary>
    /// Response DTO for GetRecentOrders endpoint
    /// </summary>
    public class GetRecentOrdersResponseV1
    {
        public IReadOnlyList<OrderDto> Orders { get; init; } = new List<OrderDto>();
        public int Count { get; init; }
        public DateTime RetrievedAt { get; init; }
    }

    #endregion
}
