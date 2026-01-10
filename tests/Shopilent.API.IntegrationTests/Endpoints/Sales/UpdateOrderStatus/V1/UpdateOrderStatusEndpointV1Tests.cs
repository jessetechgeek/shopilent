using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.UpdateOrderStatus.V1;

public class UpdateOrderStatusEndpointV1Tests : ApiIntegrationTestBase
{
    public UpdateOrderStatusEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task UpdateOrderStatus_PendingToProcessing_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Order processing started");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
        response.Data.Status.Should().Be(OrderStatus.Processing);
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdateOrderStatus_ProcessingToShipped_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToShipped("Order shipped via carrier");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task UpdateOrderStatus_ShippedToDelivered_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToDelivered("Delivery confirmed");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidData_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Testing status update");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order status changed in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Processing);
            order.Metadata.Should().NotBeNull();
            order.Metadata.Should().ContainKey("statusChange_processing_reason");
            order.Metadata["statusChange_processing_reason"].ToString().Should().Be("Testing status update");
        });
    }

    [Fact]
    public async Task UpdateOrderStatus_WithoutReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithoutReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithDetailedReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithDetailedReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Processing);
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public async Task UpdateOrderStatus_PendingToCancelled_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToCancelled("Admin cancelled order");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateOrderStatus_ProcessingToCancelled_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToCancelled("Order cancelled during processing");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateOrderStatus_ShippedToCancelled_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToCancelled("Order cancelled after shipping");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    #endregion

    #region Invalid Transition Tests

    [Fact]
    public async Task UpdateOrderStatus_DeliveredToAnyStatus_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Delivered);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Invalid transition");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot transition", "Delivered");
    }

    [Fact]
    public async Task UpdateOrderStatus_CancelledToAnyStatus_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Cancelled);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Invalid transition");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot transition", "Cancelled");
    }

    [Fact]
    public async Task UpdateOrderStatus_ProcessingToPending_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToPending("Invalid backward transition");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot transition", "Cannot update order to pending");
    }

    [Fact]
    public async Task UpdateOrderStatus_ShippedToPending_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToPending("Invalid transition");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cannot update order to pending", "Invalid");
    }

    [Fact]
    public async Task UpdateOrderStatus_PendingToShipped_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToShipped("Skip processing");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Cannot transition");
    }

    [Fact]
    public async Task UpdateOrderStatus_PendingToDelivered_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToDelivered("Skip all statuses");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Cannot transition");
    }

    [Fact]
    public async Task UpdateOrderStatus_SameStatus_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Same status");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("already", "Processing");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task UpdateOrderStatus_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Test");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateOrderStatus_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var customerAccessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(customerAccessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Customer trying to update");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateOrderStatus_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Admin update");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task UpdateOrderStatus_AsManager_ShouldReturnSuccess()
    {
        // Arrange
        var managerAccessToken = await AuthenticateAsManagerAsync();
        SetAuthenticationHeader(managerAccessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Manager update");

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task UpdateOrderStatus_WithInvalidEnumValue_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);

        var request = new
        {
            Status = 999, // Invalid enum value
            Reason = "Invalid test"
        };

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Invalid order status", "order status");
    }

    [Fact]
    public async Task UpdateOrderStatus_WithTooLongReason_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithTooLongReason(OrderStatus.Processing);

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("500 characters", "must not exceed");
    }

    [Fact]
    public async Task UpdateOrderStatus_WithMaximumLengthReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithMaximumLengthReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithEmptyReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithEmptyReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithWhitespaceReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithWhitespaceReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task UpdateOrderStatus_WithUnicodeReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithUnicodeReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithSpecialCharactersReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithSpecialCharactersReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithMultilineReason_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestWithMultilineReason(OrderStatus.Processing);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task UpdateOrderStatus_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Test");

        // Act
        var response = await PutAsync($"v1/orders/{nonExistentOrderId}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task UpdateOrderStatus_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Test");

        // Act
        var response = await PutAsync("v1/orders/invalid-guid/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Common Status Update Reasons Tests

    [Theory]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.ProcessingStarted)]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.QualityCheckPassed)]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.ManualReview)]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.AdminOverride)]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.SystemUpdate)]
    [InlineData(OrderTestDataV1.StatusUpdate.CommonReasons.InventoryConfirmed)]
    public async Task UpdateOrderStatus_WithCommonReasons_ShouldReturnSuccess(string reason)
    {
        // Arrange
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateValidRequest(OrderStatus.Processing, reason);

        // Act
        var response = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Status.Should().Be(OrderStatus.Processing);
    }

    #endregion

    #region Multiple Status Transition Tests

    [Fact]
    public async Task UpdateOrderStatus_FullWorkflow_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);

        // Act & Assert - Pending to Processing
        var request1 = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Start processing");
        var response1 = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request1);
        AssertApiSuccess(response1);
        response1!.Data.Status.Should().Be(OrderStatus.Processing);

        // Act & Assert - Processing to Shipped
        var request2 = OrderTestDataV1.StatusUpdate.CreateRequestToShipped("Order shipped");
        var response2 = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request2);
        AssertApiSuccess(response2);
        response2!.Data.Status.Should().Be(OrderStatus.Shipped);

        // Act & Assert - Shipped to Delivered
        var request3 = OrderTestDataV1.StatusUpdate.CreateRequestToDelivered("Order delivered");
        var response3 = await PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
            $"v1/orders/{orderId}/status", request3);
        AssertApiSuccess(response3);
        response3!.Data.Status.Should().Be(OrderStatus.Delivered);

        // Verify final state in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Delivered);
        });
    }

    #endregion

    #region Concurrent Update Tests

    [Fact]
    public async Task UpdateOrderStatus_ConcurrentUpdates_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.StatusUpdate.CreateRequestToProcessing("Concurrent test");

        // Act - Send multiple concurrent status update requests
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PutApiResponseAsync<object, UpdateOrderStatusResponseV1>(
                $"v1/orders/{orderId}/status", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - At least one should succeed
        var successfulResponses = responses.Where(r => r?.Succeeded == true).ToList();
        successfulResponses.Should().NotBeEmpty("at least one concurrent request should succeed");

        // Verify final state in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Processing);
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test order for the customer user with specified status
    /// </summary>
    private async Task<Guid> CreateTestOrderAsync(OrderStatus status)
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            // Get the existing customer user
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException("Customer user not found. Ensure AuthenticateAsCustomerAsync() or EnsureCustomerUserExistsAsync() is called first.");
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
                description: "Test product for order status update",
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

    public class UpdateOrderStatusResponseV1
    {
        public Guid Id { get; init; }
        public OrderStatus Status { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    #endregion
}
