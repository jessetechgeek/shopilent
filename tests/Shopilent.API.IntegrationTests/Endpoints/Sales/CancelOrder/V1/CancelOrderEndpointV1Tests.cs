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

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.CancelOrder.V1;

public class CancelOrderEndpointV1Tests : ApiIntegrationTestBase
{
    public CancelOrderEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task CancelOrder_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Customer requested cancellation");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.OrderId.Should().Be(orderId);
        response.Data.Status.Should().Be(OrderStatus.Cancelled);
        response.Data.Reason.Should().Be("Customer requested cancellation");
        response.Data.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CancelOrder_WithValidData_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Testing cancellation");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order status changed in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Cancelled);
            order.Metadata.Should().ContainKey("cancellationReason");
            order.Metadata["cancellationReason"].ToString().Should().Be("Testing cancellation");
        });
    }

    [Fact]
    public async Task CancelOrder_WithoutReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithoutReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.OrderId.Should().Be(orderId);
        response.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_WithDetailedReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithDetailedReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("Customer changed their mind");
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task CancelOrder_PendingOrder_AsCustomer_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Pending order cancellation");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ProcessingOrder_AsCustomer_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Processing order cancellation");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ShippedOrder_AsCustomer_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Trying to cancel shipped order");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("only pending or processing orders can be cancelled");
    }

    [Fact]
    public async Task CancelOrder_DeliveredOrder_AsCustomer_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Delivered);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Trying to cancel delivered order");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("only pending or processing orders can be cancelled");
    }

    [Fact]
    public async Task CancelOrder_AlreadyCancelledOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Cancelled);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Already cancelled");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert - Should be idempotent
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    #endregion

    #region Admin/Manager Permission Tests

    [Fact]
    public async Task CancelOrder_ShippedOrder_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();

        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Admin cancelling shipped order");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_DeliveredOrder_AsAdmin_ShouldReturnBadRequest()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();

        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Delivered);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Admin trying to cancel delivered");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("delivered orders cannot be cancelled", "Invalid order status");
    }

    [Fact]
    public async Task CancelOrder_AnotherCustomersOrder_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();

        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest(
            OrderTestDataV1.Cancellation.CommonReasons.FraudulentOrder);

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Be(OrderTestDataV1.Cancellation.CommonReasons.FraudulentOrder);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CancelOrder_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Test");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOrder_OtherCustomersOrder_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        // First ensure customer user exists and create order for them
        await AuthenticateAsCustomerAsync();
        var otherCustomerOrderId = await CreateTestOrderForCustomerAsync(OrderStatus.Pending);

        // Create and authenticate as a different customer
        await RegisterSecondCustomerAsync();
        var secondCustomerAccessToken = await AuthenticateAsync("customer2@shopilent.com", "Customer123!");
        SetAuthenticationHeader(secondCustomerAccessToken);

        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Trying to cancel another's order");

        // Act
        var response = await PostAsync($"v1/orders/{otherCustomerOrderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not authorized", "forbidden");
    }

    [Fact]
    public async Task CancelOrder_OwnOrder_AsCustomer_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Cancelling my own order");

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task CancelOrder_WithTooLongReason_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithTooLongReason();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("500 characters", "must not exceed");
    }

    [Fact]
    public async Task CancelOrder_WithMaximumLengthReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithMaximumLengthReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().HaveLength(500);
    }

    [Fact]
    public async Task CancelOrder_WithEmptyReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithEmptyReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task CancelOrder_WithWhitespaceReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithWhitespaceReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task CancelOrder_WithUnicodeReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithUnicodeReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("顧客がキャンセル");
        response.Data.Reason.Should().Contain("🛍️");
    }

    [Fact]
    public async Task CancelOrder_WithSpecialCharactersReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithSpecialCharactersReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("@#$%^&*()");
    }

    [Fact]
    public async Task CancelOrder_WithMultilineReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateRequestWithMultilineReason();

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("\n");
    }

    [Fact]
    public async Task CancelOrder_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Test");

        // Act
        var response = await PostAsync($"v1/orders/{nonExistentOrderId}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task CancelOrder_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Test");

        // Act
        var response = await PostAsync("v1/orders/invalid-guid/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Common Cancellation Reasons Tests

    [Theory]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.CustomerRequest)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.OutOfStock)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.PaymentFailed)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.DuplicateOrder)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.FraudulentOrder)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.CustomerNoResponse)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.ShippingIssue)]
    [InlineData(OrderTestDataV1.Cancellation.CommonReasons.PriceError)]
    public async Task CancelOrder_WithCommonReasons_ShouldReturnSuccess(string reason)
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();

        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest(reason);

        // Act
        var response = await PostApiResponseAsync<object, CancelOrderResponseV1>(
            $"v1/orders/{orderId}/cancel", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Be(reason);
    }

    #endregion

    #region Concurrent Cancellation Tests

    [Fact]
    public async Task CancelOrder_ConcurrentCancellations_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Cancellation.CreateValidRequest("Concurrent test");

        // Act - Send multiple concurrent cancellation requests for the same order
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PostApiResponseAsync<object, CancelOrderResponseV1>(
                $"v1/orders/{orderId}/cancel", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - At least one should succeed, others may fail with concurrency conflict
        var successfulResponses = responses.Where(r => r?.Succeeded == true).ToList();
        successfulResponses.Should().NotBeEmpty("at least one concurrent request should succeed");
        successfulResponses.Should().AllSatisfy(response =>
        {
            response!.Data.Status.Should().Be(OrderStatus.Cancelled);
        });

        // Verify final state in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Cancelled);
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Registers a second customer user for testing cross-customer scenarios
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
        // Ignore if user already exists (409 Conflict) - that's expected after first test
    }

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
                description: "Test product for order cancellation",
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
            var snapshot = ProductSnapshot.Create(product.Name, product.Sku, product.Slug?.Value).Value;
            order.AddItem(product.Id, null, 1, productPrice, snapshot);

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

    public class CancelOrderResponseV1
    {
        public Guid OrderId { get; init; }
        public OrderStatus Status { get; init; }
        public string Reason { get; init; }
        public DateTime CancelledAt { get; init; }
    }

    #endregion
}
