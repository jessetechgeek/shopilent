using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.ProcessOrderRefund.V1;

public class ProcessOrderRefundEndpointV1Tests : ApiIntegrationTestBase
{
    public ProcessOrderRefundEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task ProcessOrderRefund_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Customer requested refund");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.OrderId.Should().Be(orderId);
        response.Data.Status.Should().Be("Refunded");
        response.Data.Reason.Should().Be("Customer requested refund");
        response.Data.RefundAmount.Should().BeGreaterThan(0);
        response.Data.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessOrderRefund_WithValidData_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Testing refund");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order refund details in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Cancelled);
            order.RefundedAmount.Should().NotBeNull();
            order.RefundedAmount.Amount.Should().BeGreaterThan(0);
            order.RefundReason.Should().Be("Testing refund");
            order.RefundedAt.Should().NotBeNull();
            order.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task ProcessOrderRefund_WithoutReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithoutReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task ProcessOrderRefund_WithDetailedReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithDetailedReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("Product did not meet quality expectations");
    }

    [Fact]
    public async Task ProcessOrderRefund_FullRefund_ShouldMatchOrderTotal()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Full refund");

        // Get original order total
        var originalTotal = await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            return order!.Total.Amount;
        });

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.RefundAmount.Should().Be(originalTotal);
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task ProcessOrderRefund_PendingOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending, PaymentStatus.Pending);
        var request = OrderTestDataV1.Refund.CreateValidRequest("Trying to refund pending order");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Cannot perform refund operation with current order status");
    }

    [Fact]
    public async Task ProcessOrderRefund_ProcessingPaidOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync(); // Processing + Paid
        var request = OrderTestDataV1.Refund.CreateValidRequest("Refunding processing order");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task ProcessOrderRefund_ShippedPaidOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.Refund.CreateValidRequest("Refunding shipped order");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task ProcessOrderRefund_DeliveredOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Delivered, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.Refund.CreateValidRequest("Refunding delivered order");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be("Refunded"); // Delivered → Cancelled after refund
        response.Data.RefundAmount.Should().BeGreaterThan(0);
        response.Data.Reason.Should().Be("Refunding delivered order");
    }

    [Fact]
    public async Task ProcessOrderRefund_AlreadyRefundedOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // First refund
        var firstRequest = OrderTestDataV1.Refund.CreateValidRequest("First refund");
        var firstResponse = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", firstRequest);
        AssertApiSuccess(firstResponse);

        // Try to refund again
        var secondRequest = OrderTestDataV1.Refund.CreateValidRequest("Second refund attempt");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("already been fully refunded");
    }

    [Fact]
    public async Task ProcessOrderRefund_CancelledOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Cancelled, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.Refund.CreateValidRequest("Trying to refund cancelled order");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Cannot perform refund a cancelled order operation with current order status");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task ProcessOrderRefund_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Test");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProcessOrderRefund_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Customer trying to refund");

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProcessOrderRefund_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Admin processing refund");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ProcessOrderRefund_WithTooLongReason_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithTooLongReason();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("500 characters", "must not exceed");
    }

    [Fact]
    public async Task ProcessOrderRefund_WithMaximumLengthReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithMaximumLengthReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().HaveLength(500);
    }

    [Fact]
    public async Task ProcessOrderRefund_WithEmptyReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithEmptyReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderRefund_WithWhitespaceReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithWhitespaceReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ProcessOrderRefund_WithUnicodeReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithUnicodeReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("返金処理");
        response.Data.Reason.Should().Contain("💰");
    }

    [Fact]
    public async Task ProcessOrderRefund_WithSpecialCharactersReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithSpecialCharactersReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("@#$%^&*()");
    }

    [Fact]
    public async Task ProcessOrderRefund_WithMultilineReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateRequestWithMultilineReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("\n");
    }

    [Fact]
    public async Task ProcessOrderRefund_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Test");

        // Act
        var response = await PostAsync($"v1/orders/{nonExistentOrderId}/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task ProcessOrderRefund_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.Refund.CreateValidRequest("Test");

        // Act
        var response = await PostAsync("v1/orders/invalid-guid/refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Common Refund Reasons Tests

    [Theory]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.CustomerRequest)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.DefectiveProduct)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.WrongItemShipped)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.ProductNotAsDescribed)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.DamagedInShipping)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.QualityNotAcceptable)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.LateDelivery)]
    [InlineData(OrderTestDataV1.Refund.CommonReasons.CustomerChangedMind)]
    public async Task ProcessOrderRefund_WithCommonReasons_ShouldReturnSuccess(string reason)
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest(reason);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Be(reason);
    }

    #endregion

    #region Currency and Amount Tests

    [Fact]
    public async Task ProcessOrderRefund_ShouldReturnCorrectCurrency()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Currency check");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task ProcessOrderRefund_ShouldPreserveRefundInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var refundReason = "Detailed refund reason for audit trail";
        var request = OrderTestDataV1.Refund.CreateValidRequest(refundReason);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderRefundResponseV1>(
            $"v1/orders/{orderId}/refund", request);

        // Assert
        AssertApiSuccess(response);

        // Verify all refund details are stored
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.RefundReason.Should().Be(refundReason);
            order.RefundedAmount.Should().NotBeNull();
            order.RefundedAt.Should().NotBeNull();
        });
    }

    #endregion

    #region Concurrent Refund Tests

    [Fact]
    public async Task ProcessOrderRefund_ConcurrentRefunds_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.Refund.CreateValidRequest("Concurrent test");

        // Act - Send multiple concurrent refund requests for the same order
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PostAsync($"v1/orders/{orderId}/refund", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - Only one should succeed
        var successfulResponses = responses.Where(r => r.IsSuccessStatusCode).ToList();
        successfulResponses.Should().HaveCountLessThanOrEqualTo(1, "only one concurrent refund request should succeed");

        // Verify final state in database - order should be refunded only once
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Cancelled);
            order.RefundedAt.Should().NotBeNull();
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a paid order ready for refund testing
    /// </summary>
    private async Task<Guid> CreatePaidOrderAsync()
    {
        return await CreateTestOrderAsync(OrderStatus.Processing, PaymentStatus.Succeeded);
    }

    /// <summary>
    /// Creates a test order with specified status and payment status
    /// </summary>
    private async Task<Guid> CreateTestOrderAsync(OrderStatus status, PaymentStatus paymentStatus)
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            // Get or create admin user
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "admin@shopilent.com");
            if (user == null)
            {
                var email = Email.Create("admin@shopilent.com").Value;
                var fullName = FullName.Create("Admin", "User").Value;
                user = User.Create(email, "Admin", fullName).Value;
                context.Users.Add(user);
                await context.SaveChangesAsync();
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
                description: "Test product for refund",
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create order
            var order = Order.Create(
                user: user,
                shippingAddress: address,
                billingAddress: address,
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
        await UpdateOrderStatus(orderId, status, paymentStatus);

        return orderId;
    }

    private async Task UpdateOrderStatus(Guid orderId, OrderStatus targetStatus, PaymentStatus paymentStatus)
    {
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order == null) return;

            // Set payment status first if needed
            if (paymentStatus == PaymentStatus.Succeeded)
            {
                order.MarkAsPaid();
            }

            // Then set order status
            switch (targetStatus)
            {
                case OrderStatus.Processing:
                    // Already processing after MarkAsPaid
                    break;
                case OrderStatus.Shipped:
                    if (order.PaymentStatus == PaymentStatus.Succeeded)
                    {
                        order.MarkAsShipped();
                    }

                    break;
                case OrderStatus.Delivered:
                    if (order.PaymentStatus == PaymentStatus.Succeeded)
                    {
                        order.MarkAsShipped();
                        order.MarkAsDelivered();
                    }

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

    public class ProcessOrderRefundResponseV1
    {
        public Guid OrderId { get; init; }
        public decimal RefundAmount { get; init; }
        public string Currency { get; init; }
        public string Reason { get; init; }
        public DateTime RefundedAt { get; init; }
        public string Status { get; init; }
    }

    #endregion
}
