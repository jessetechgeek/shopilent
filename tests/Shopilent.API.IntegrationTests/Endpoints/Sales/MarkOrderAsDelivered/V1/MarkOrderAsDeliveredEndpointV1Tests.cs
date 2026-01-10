using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.MarkOrderAsDelivered.V1;

public class MarkOrderAsDeliveredEndpointV1Tests : ApiIntegrationTestBase
{
    public MarkOrderAsDeliveredEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task MarkOrderAsDelivered_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
        response.Data.Status.Should().Be(OrderStatus.Delivered);
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MarkOrderAsDelivered_WithValidData_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(response);

        // Verify order status changed in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Delivered);
        });
    }

    [Fact]
    public async Task MarkOrderAsDelivered_AsManager_ShouldReturnSuccess()
    {
        // Arrange - Use built-in EnsureManagerUserExistsAsync which properly sets role
        var managerToken = await AuthenticateAsManagerAsync();
        SetAuthenticationHeader(managerToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Shipped);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Delivered);
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task MarkOrderAsDelivered_ShippedOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public async Task MarkOrderAsDelivered_PendingOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("invalid", "status", "shipped");
    }

    [Fact]
    public async Task MarkOrderAsDelivered_ProcessingOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("invalid", "status", "shipped");
    }

    [Fact]
    public async Task MarkOrderAsDelivered_AlreadyDeliveredOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Delivered);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert - Should be idempotent
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public async Task MarkOrderAsDelivered_CancelledOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Cancelled);

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("invalid", "status", "cancelled");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task MarkOrderAsDelivered_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkOrderAsDelivered_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var customerToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkOrderAsDelivered_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act
        var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task MarkOrderAsDelivered_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();

        // Act
        var response = await PutAsync($"v1/orders/{nonExistentOrderId}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task MarkOrderAsDelivered_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await PutAsync("v1/orders/invalid-guid/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkOrderAsDelivered_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await PutAsync($"v1/orders/{Guid.Empty}/delivered", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task MarkOrderAsDelivered_MultipleOrdersSequentially_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();

        var orderIds = new List<Guid>
        {
            await CreateTestOrderAsync(OrderStatus.Shipped),
            await CreateTestOrderAsync(OrderStatus.Shipped),
            await CreateTestOrderAsync(OrderStatus.Shipped)
        };

        // Act - Mark all orders as delivered sequentially
        var responses = new List<ApiResponse<MarkOrderAsDeliveredResponseV1>>();
        foreach (var orderId in orderIds)
        {
            var response = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
                $"v1/orders/{orderId}/delivered", new { });
            responses.Add(response!);
        }

        // Assert
        responses.Should().AllSatisfy(response => AssertApiSuccess(response));
        responses.Select(r => r.Data.Id).Should().BeEquivalentTo(orderIds);
        responses.Should().AllSatisfy(response =>
        {
            response.Data.Status.Should().Be(OrderStatus.Delivered);
        });
    }

    [Fact]
    public async Task MarkOrderAsDelivered_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);

        // Act - Send multiple concurrent requests for the same order
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
                $"v1/orders/{orderId}/delivered", new { }))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - At least one should succeed (idempotent operation)
        // Some may fail with concurrency conflicts, but the operation should complete
        var successfulResponses = responses.Where(r => r?.Succeeded == true).ToList();
        successfulResponses.Should().NotBeEmpty("at least one concurrent request should succeed");

        // All successful responses should have correct status
        successfulResponses.Should().AllSatisfy(response =>
        {
            response!.Data.Status.Should().Be(OrderStatus.Delivered);
        });

        // Verify final state in database - order should be delivered
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Delivered);
        });
    }

    [Fact]
    public async Task MarkOrderAsDelivered_OrderTransitionFromPendingToDelivered_ShouldRequireMultipleSteps()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);

        // Act - Try to mark pending order as delivered (should fail)
        var deliveredResponse = await PutAsync($"v1/orders/{orderId}/delivered", new { });

        // Assert
        deliveredResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Act - Mark as paid first (transitions to Processing)
        await UpdateOrderStatus(orderId, OrderStatus.Processing);

        // Try to mark processing order as delivered (should still fail)
        var deliveredResponse2 = await PutAsync($"v1/orders/{orderId}/delivered", new { });
        deliveredResponse2.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Act - Mark as shipped first
        await UpdateOrderStatus(orderId, OrderStatus.Shipped);

        // Now mark as delivered (should succeed)
        var finalResponse = await PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
            $"v1/orders/{orderId}/delivered", new { });

        // Assert
        AssertApiSuccess(finalResponse);
        finalResponse!.Data.Status.Should().Be(OrderStatus.Delivered);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task MarkOrderAsDelivered_MultipleOrdersConcurrently_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();

        // Create 5 test orders
        var orderIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            orderIds.Add(await CreateTestOrderAsync(OrderStatus.Shipped));
        }

        // Act - Mark all orders as delivered concurrently
        var tasks = orderIds
            .Select(orderId => PutApiResponseAsync<object, MarkOrderAsDeliveredResponseV1>(
                $"v1/orders/{orderId}/delivered", new { }))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response => AssertApiSuccess(response));
        responses.Select(r => r!.Data.Id).Should().OnlyHaveUniqueItems();
        responses.Should().AllSatisfy(response =>
        {
            response!.Data.Status.Should().Be(OrderStatus.Delivered);
        });

        // Verify all orders are delivered in database
        await ExecuteDbContextAsync(async context =>
        {
            var orders = await context.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            orders.Should().HaveCount(5);
            orders.Should().AllSatisfy(order =>
            {
                order.Status.Should().Be(OrderStatus.Delivered);
            });
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test order for the currently authenticated customer user
    /// </summary>
    private async Task<Guid> CreateTestOrderAsync(OrderStatus status)
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            // Get the existing customer user
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException("Customer user not found. Ensure AuthenticateAsCustomerAsync() is called first.");
            }

            // Create address
            var postalAddress = Domain.Shipping.ValueObjects.PostalAddress.Create(
                addressLine1: "123 Test St",
                city: "Test City",
                state: "CA",
                country: "United States",
                postalCode: "90210"
            ).Value;

            var phoneNumber = PhoneNumber.Create("555-0123").Value;
            var address = Domain.Shipping.Address.CreateShipping(user.Id, postalAddress, phoneNumber, false).Value;
            context.Addresses.Add(address);
            await context.SaveChangesAsync();

            // Create product
            var productSlug = Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = Money.Create(99.99m, "USD").Value;
            var product = Product.CreateWithDescription(
                name: $"Test Product {Guid.NewGuid():N}",
                slug: productSlug,
                basePrice: productPrice,
                description: "Test product for order delivery",
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create order
            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            // Add an item to the order
            order.AddItem(product, 1, productPrice);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });

        // Update order status as needed
        await UpdateOrderStatus(orderId, status);

        return orderId;
    }

    /// <summary>
    /// Creates a test order for the default customer (not the currently authenticated user)
    /// </summary>
    private async Task<Guid> CreateTestOrderForCustomerAsync(OrderStatus status)
    {
        // Reuse the same method since both are for the customer@shopilent.com user
        return await CreateTestOrderAsync(status);
    }

    private async Task UpdateOrderStatus(Guid orderId, OrderStatus targetStatus)
    {
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order == null) return;

            switch (targetStatus)
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
    }

    #endregion

    #region Response DTO

    public class MarkOrderAsDeliveredResponseV1
    {
        public Guid Id { get; init; }
        public OrderStatus Status { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    #endregion
}
