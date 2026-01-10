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

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.MarkOrderAsShipped.V1;

public class MarkOrderAsShippedEndpointV1Tests : ApiIntegrationTestBase
{
    public MarkOrderAsShippedEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task MarkOrderAsShipped_WithValidTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("1Z999AA10123456784");

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNullOrEmpty();
        response.Data.Should().Contain("Order marked as shipped");
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithValidTrackingNumber_ShouldUpdateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var trackingNumber = "1Z999AA10123456784";
        var request = OrderTestDataV1.Shipping.CreateValidRequest(trackingNumber);

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order status changed in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Shipped);
            order.Metadata.Should().ContainKey("trackingNumber");
            order.Metadata["trackingNumber"].ToString().Should().Be(trackingNumber);
        });
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithoutTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithoutTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().Contain("Order marked as shipped successfully");
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithEmptyTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithEmptyTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Order Status Tests

    [Fact]
    public async Task MarkOrderAsShipped_ProcessingOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order status in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Shipped);
        });
    }

    [Fact]
    public async Task MarkOrderAsShipped_PendingOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Pending);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("status", "cannot be marked as shipped");
    }

    [Fact]
    public async Task MarkOrderAsShipped_AlreadyShippedOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Shipped);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("NEW-TRACK-456");

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert - Should be idempotent
        AssertApiSuccess(response);

        // Verify order status remains shipped
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Shipped);
        });
    }

    [Fact]
    public async Task MarkOrderAsShipped_DeliveredOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Delivered);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("delivered", "already delivered");
    }

    [Fact]
    public async Task MarkOrderAsShipped_CancelledOrder_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Cancelled);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("cancelled", "cannot be shipped");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task MarkOrderAsShipped_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkOrderAsShipped_AsCustomer_ShouldReturnForbidden()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkOrderAsShipped_AsAdmin_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task MarkOrderAsShipped_WithTooLongTrackingNumber_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithTooLongTrackingNumber();

        // Act
        var response = await PutAsync($"v1/orders/{orderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("100 characters", "must not exceed");
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithMaximumLengthTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithMaximumLengthTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithWhitespaceTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithWhitespaceTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task MarkOrderAsShipped_WithUnicodeTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithUnicodeTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithSpecialCharactersTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithSpecialCharactersTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithNumericTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithNumericTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithAlphanumericTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithAlphanumericTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithShortTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithShortTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_NonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();
        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync($"v1/orders/{nonExistentOrderId}/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.Shipping.CreateValidRequest("TRACK123456");

        // Act
        var response = await PutAsync("v1/orders/invalid-guid/shipped", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Common Tracking Number Format Tests

    [Theory]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.UPS)]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.FedEx)]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.USPS)]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.DHL)]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.AmazonLogistics)]
    [InlineData(OrderTestDataV1.Shipping.CommonFormats.CanadaPost)]
    public async Task MarkOrderAsShipped_WithCommonCarrierFormats_ShouldReturnSuccess(string trackingNumber)
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest(trackingNumber);

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);

        // Verify tracking number stored correctly
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            order.Should().NotBeNull();
            order!.Status.Should().Be(OrderStatus.Shipped);
            order.Metadata.Should().ContainKey("trackingNumber");
            order.Metadata["trackingNumber"].ToString().Should().Be(trackingNumber);
        });
    }

    #endregion

    #region Specific Carrier Tests

    [Fact]
    public async Task MarkOrderAsShipped_WithUpsTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithUpsTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
        // Tracking number is in the message field, not data
        response!.Message.Should().Contain("1Z999AA10123456784");
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithFedExTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithFedExTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithUspsTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithUspsTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WithDhlTrackingNumber_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateRequestWithDhlTrackingNumber();

        // Act
        var response = await PutApiResponseAsync<object, string>(
            $"v1/orders/{orderId}/shipped", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Concurrent Shipping Tests

    [Fact]
    public async Task MarkOrderAsShipped_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var request = OrderTestDataV1.Shipping.CreateValidRequest("CONCURRENT-TRACK-001");

        // Act - Send multiple concurrent requests for the same order
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => PutApiResponseAsync<object, string>(
                $"v1/orders/{orderId}/shipped", request))
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
            order!.Status.Should().Be(OrderStatus.Shipped);
        });
    }

    #endregion

    #region Multiple Orders Tests

    [Fact]
    public async Task MarkOrderAsShipped_MultipleOrders_ShouldMarkEachIndependently()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        await EnsureCustomerUserExistsAsync();
        var orderId1 = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var orderId2 = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);
        var orderId3 = await CreateTestOrderForCustomerAsync(OrderStatus.Processing);

        var request1 = OrderTestDataV1.Shipping.CreateValidRequest("TRACK-001");
        var request2 = OrderTestDataV1.Shipping.CreateValidRequest("TRACK-002");
        var request3 = OrderTestDataV1.Shipping.CreateValidRequest("TRACK-003");

        // Act
        var response1 = await PutApiResponseAsync<object, string>($"v1/orders/{orderId1}/shipped", request1);
        var response2 = await PutApiResponseAsync<object, string>($"v1/orders/{orderId2}/shipped", request2);
        var response3 = await PutApiResponseAsync<object, string>($"v1/orders/{orderId3}/shipped", request3);

        // Assert
        AssertApiSuccess(response1);
        AssertApiSuccess(response2);
        AssertApiSuccess(response3);

        // Verify all orders marked as shipped with correct tracking numbers
        await ExecuteDbContextAsync(async context =>
        {
            var order1 = await context.Orders.FindAsync(orderId1);
            var order2 = await context.Orders.FindAsync(orderId2);
            var order3 = await context.Orders.FindAsync(orderId3);

            order1.Should().NotBeNull();
            order1!.Status.Should().Be(OrderStatus.Shipped);
            order1.Metadata["trackingNumber"].ToString().Should().Be("TRACK-001");

            order2.Should().NotBeNull();
            order2!.Status.Should().Be(OrderStatus.Shipped);
            order2.Metadata["trackingNumber"].ToString().Should().Be("TRACK-002");

            order3.Should().NotBeNull();
            order3!.Status.Should().Be(OrderStatus.Shipped);
            order3.Metadata["trackingNumber"].ToString().Should().Be("TRACK-003");
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
                description: "Test product for order shipping",
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
}
