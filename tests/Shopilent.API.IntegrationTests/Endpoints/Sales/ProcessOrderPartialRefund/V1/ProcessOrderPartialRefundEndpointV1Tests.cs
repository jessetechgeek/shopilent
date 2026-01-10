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

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.ProcessOrderPartialRefund.V1;

public class ProcessOrderPartialRefundEndpointV1Tests : ApiIntegrationTestBase
{
    public ProcessOrderPartialRefundEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(
            amount: 25.00m,
            reason: "Minor product defect - partial compensation");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.OrderId.Should().Be(orderId);
        response.Data.RefundAmount.Should().Be(25.00m);
        response.Data.Currency.Should().Be("USD");
        response.Data.Reason.Should().Be("Minor product defect - partial compensation");
        response.Data.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.IsFullyRefunded.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithValidData_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(
            amount: 30.00m,
            reason: "Testing partial refund");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order partial refund details in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Processing); // Should remain Processing, not Cancelled
            order.RefundedAmount.Should().NotBeNull();
            order.RefundedAmount!.Amount.Should().Be(30.00m);
            // For partial refunds, reason is stored in Metadata, not RefundReason property
            order.RefundedAt.Should().NotBeNull();
            order.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

            // Verify partial refund details in metadata
            order.Metadata.Should().ContainKey("partialRefunds");
        });
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithoutReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithoutReason(20.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.OrderId.Should().Be(orderId);
        response.Data.RefundAmount.Should().Be(20.00m);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithDetailedReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithDetailedReason(35.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("minor product defect");
        response.Data.Reason.Should().Contain("partial compensation");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_ShouldCalculateTotalRefundedAmount()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // Get original order total
        var originalTotal = await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            return order!.Total.Amount;
        });

        var partialAmount = 25.00m;
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(partialAmount, reason: "First partial refund");

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.RefundAmount.Should().Be(partialAmount);
        response.Data.TotalRefundedAmount.Should().Be(partialAmount);
        response.Data.RemainingAmount.Should().Be(originalTotal - partialAmount);
        response.Data.IsFullyRefunded.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_MultipleTimes_ShouldAccumulateRefunds()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // First partial refund
        var firstRequest = OrderTestDataV1.PartialRefund.CreateValidRequest(20.00m, reason: "First partial refund");
        var firstResponse = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", firstRequest);
        AssertApiSuccess(firstResponse);
        firstResponse!.Data.TotalRefundedAmount.Should().Be(20.00m);

        // Verify first refund was persisted
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            order.Should().NotBeNull();
            order!.RefundedAmount.Should().NotBeNull();
            order.RefundedAmount!.Amount.Should().Be(20.00m);
        });

        // Second partial refund
        var secondRequest = OrderTestDataV1.PartialRefund.CreateValidRequest(15.00m, reason: "Second partial refund");
        var secondResponse = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", secondRequest);

        // Assert
        AssertApiSuccess(secondResponse);
        secondResponse!.Data.RefundAmount.Should().Be(15.00m);
        secondResponse.Data.TotalRefundedAmount.Should().Be(35.00m); // 20 + 15
    }

    #endregion

    #region Amount Validation Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithZeroAmount_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithZeroAmount();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithNegativeAmount_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithNegativeAmount();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("must be greater than 0");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithExcessiveDecimalPlaces_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithExcessiveDecimalPlaces();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("cannot have more than 2 decimal places");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithTwoDecimalPlaces_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithTwoDecimalPlaces();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.RefundAmount.Should().Be(25.99m);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithVerySmallAmount_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithVerySmallAmount();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.RefundAmount.Should().Be(0.01m);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_AmountExceedingOrderTotal_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // Get order total
        var orderTotal = await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            return order!.Total.Amount;
        });

        var excessiveAmount = orderTotal + 100.00m;
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(excessiveAmount);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Invalid order amount");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_AmountExceedingRemainingBalance_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // First partial refund
        var firstRequest = OrderTestDataV1.PartialRefund.CreateValidRequest(50.00m);
        await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", firstRequest);

        // Get remaining balance
        var remainingBalance = await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            return order!.Total.Amount - (order.RefundedAmount?.Amount ?? 0);
        });

        // Try to refund more than remaining
        var excessiveRequest = OrderTestDataV1.PartialRefund.CreateValidRequest(remainingBalance + 10.00m);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", excessiveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Invalid order amount");
    }

    #endregion

    #region Currency Validation Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithEmptyCurrency_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithEmptyCurrency();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Currency is required");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithInvalidCurrencyLength_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithInvalidCurrencyLength();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("3-letter ISO code");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithLowercaseCurrency_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithLowercaseCurrency();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("uppercase");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithInvalidCurrencyCharacters_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithInvalidCurrencyCharacters();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("uppercase letters only");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithValidUsdCurrency_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithCurrency("USD", 25.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Currency.Should().Be("USD");
    }

    #endregion

    #region Reason Validation Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithTooLongReason_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithTooLongReason();

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("500 characters", "cannot exceed");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithMaximumLengthReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithMaximumLengthReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().HaveLength(500);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithEmptyReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithEmptyReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithWhitespaceReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithWhitespaceReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_PendingOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending, PaymentStatus.Pending);
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot", "status", "pending");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_ProcessingPaidOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync(); // Processing + Paid
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(30.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_ShippedPaidOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_DeliveredOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Delivered, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(20.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_CancelledOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Cancelled, PaymentStatus.Succeeded);
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot", "cancelled");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync($"v1/orders/{orderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_AsManager_ShouldReturnSuccess()
    {
        // Arrange
        var managerAccessToken = await AuthenticateAsManagerAsync();
        SetAuthenticationHeader(managerAccessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_WithUnicodeReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithUnicodeReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("部分返金処理");
        response.Data.Reason.Should().Contain("💰");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithSpecialCharactersReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithSpecialCharactersReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("@#$%^&*()");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithMultilineReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateRequestWithMultilineReason();

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Contain("\n");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync($"v1/orders/{nonExistentOrderId}/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostAsync("v1/orders/invalid-guid/partial-refund", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Common Partial Refund Reasons Tests

    [Theory]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.MinorDefect)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.ShippingDelay)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.MissingAccessory)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.PriceAdjustment)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.PartialDamage)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.ServiceIssue)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.GoodwillGesture)]
    [InlineData(OrderTestDataV1.PartialRefund.CommonReasons.IncompleteOrder)]
    public async Task ProcessOrderPartialRefund_WithCommonReasons_ShouldReturnSuccess(string reason)
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(15.00m, reason: reason);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Reason.Should().Be(reason);
    }

    #endregion

    #region Response Data Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_ShouldReturnCorrectCurrency()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(25.00m);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_ShouldPreserveRefundInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var refundAmount = 35.00m;
        var refundReason = "Detailed partial refund reason for audit trail";
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(refundAmount, reason: refundReason);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);

        // Verify all partial refund details are stored
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            // For partial refunds, reason is stored in Metadata["partialRefunds"], not RefundReason
            order!.RefundedAmount.Should().NotBeNull();
            order.RefundedAmount!.Amount.Should().Be(refundAmount);
            order.RefundedAt.Should().NotBeNull();
            order.Metadata.Should().ContainKey("partialRefunds");
        });
    }

    [Fact]
    public async Task ProcessOrderPartialRefund_FullOrderAmount_ShouldMarkAsFullyRefunded()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();

        // Get order total
        var orderTotal = await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            return order!.Total.Amount;
        });

        var refundReason = "Full refund via partial refund endpoint";
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(orderTotal, reason: refundReason);

        // Act
        var response = await PostApiResponseAsync<object, ProcessOrderPartialRefundResponseV1>(
            $"v1/orders/{orderId}/partial-refund", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.TotalRefundedAmount.Should().Be(orderTotal);
        response.Data.RemainingAmount.Should().Be(0);
        response.Data.IsFullyRefunded.Should().BeTrue();

        // When partial refund equals total, order should be cancelled and RefundReason should be set
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Cancelled);
            order.PaymentStatus.Should().Be(PaymentStatus.Refunded);
            order.RefundReason.Should().Be(refundReason); // Now RefundReason should be set
        });
    }

    #endregion

    #region Concurrent Partial Refund Tests

    [Fact]
    public async Task ProcessOrderPartialRefund_ConcurrentRefunds_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreatePaidOrderAsync();
        var request = OrderTestDataV1.PartialRefund.CreateValidRequest(10.00m);

        // Act - Send multiple concurrent partial refund requests for the same order
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PostAsync($"v1/orders/{orderId}/partial-refund", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed or fail gracefully (no crashes)
        var successfulResponses = responses.Where(r => r.IsSuccessStatusCode).ToList();
        successfulResponses.Should().HaveCountGreaterThan(0, "at least one concurrent partial refund request should succeed");

        // Verify final state in database - order should have valid refund data
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.RefundedAmount.Should().NotBeNull();
            order.RefundedAmount!.Amount.Should().BeGreaterThan(0);
            order.RefundedAt.Should().NotBeNull();
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a paid order ready for partial refund testing
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
                description: "Test product for partial refund",
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

    public class ProcessOrderPartialRefundResponseV1
    {
        public Guid OrderId { get; init; }
        public decimal RefundAmount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public decimal TotalRefundedAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DateTime RefundedAt { get; init; }
        public bool IsFullyRefunded { get; init; }
    }

    #endregion
}
