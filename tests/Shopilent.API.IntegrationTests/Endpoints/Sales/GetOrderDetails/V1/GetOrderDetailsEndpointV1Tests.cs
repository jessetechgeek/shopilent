using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.GetOrderDetails.V1;

public class GetOrderDetailsEndpointV1Tests : ApiIntegrationTestBase
{
    public GetOrderDetailsEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetOrderDetails_WithValidOrderId_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
        response.Data.UserId.Should().NotBeNull();
        response.Data.Status.Should().Be(default(OrderStatus));
        response.Data.PaymentStatus.Should().Be(default(PaymentStatus));
        response.Data.Items.Should().NotBeNull();
        response.Data.Items.Should().NotBeEmpty();
        response.Data.CreatedAt.Should().NotBe(default(DateTime));
    }

    [Fact]
    public async Task GetOrderDetails_WithValidOrderId_ShouldReturnCompleteOrderDetails()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        // Verify basic order information
        orderDetails.Id.Should().Be(orderId);
        orderDetails.UserId.Should().NotBeNull();
        orderDetails.ShippingAddressId.Should().NotBeNull();
        orderDetails.BillingAddressId.Should().NotBeNull();

        // Verify financial information
        orderDetails.Subtotal.Should().BeGreaterThan(0);
        orderDetails.Tax.Should().BeGreaterThanOrEqualTo(0);
        orderDetails.ShippingCost.Should().BeGreaterThanOrEqualTo(0);
        orderDetails.Total.Should().BeGreaterThan(0);
        orderDetails.Currency.Should().NotBeNullOrEmpty();

        // Verify status information
        orderDetails.Status.Should().Be(default(OrderStatus));
        orderDetails.PaymentStatus.Should().Be(default(PaymentStatus));
        orderDetails.ShippingMethod.Should().NotBeNullOrEmpty();

        // Verify related data
        orderDetails.Items.Should().NotBeNull();
        orderDetails.Items.Should().NotBeEmpty();
        orderDetails.ShippingAddress.Should().NotBeNull();
        orderDetails.BillingAddress.Should().NotBeNull();

        // Verify timestamps
        orderDetails.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        orderDetails.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetOrderDetails_WithValidOrderId_ShouldIncludeOrderItems()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderWithMultipleItemsAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.Items.Should().NotBeNull();
        orderDetails.Items.Should().NotBeEmpty();
        orderDetails.Items.Should().AllSatisfy(item =>
        {
            item.ProductId.Should().NotBeEmpty();
            item.Quantity.Should().BeGreaterThan(0);
            item.UnitPrice.Should().BeGreaterThan(0);
            item.TotalPrice.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task GetOrderDetails_WithValidOrderId_ShouldIncludeShippingAndBillingAddresses()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        // Verify shipping address
        orderDetails.ShippingAddress.Should().NotBeNull();
        orderDetails.ShippingAddress.Id.Should().NotBeEmpty();
        orderDetails.ShippingAddress.AddressLine1.Should().NotBeNullOrEmpty();
        orderDetails.ShippingAddress.City.Should().NotBeNullOrEmpty();
        orderDetails.ShippingAddress.State.Should().NotBeNullOrEmpty();
        orderDetails.ShippingAddress.Country.Should().NotBeNullOrEmpty();
        orderDetails.ShippingAddress.PostalCode.Should().NotBeNullOrEmpty();

        // Verify billing address
        orderDetails.BillingAddress.Should().NotBeNull();
        orderDetails.BillingAddress.Id.Should().NotBeEmpty();
        orderDetails.BillingAddress.AddressLine1.Should().NotBeNullOrEmpty();
        orderDetails.BillingAddress.City.Should().NotBeNullOrEmpty();
        orderDetails.BillingAddress.State.Should().NotBeNullOrEmpty();
        orderDetails.BillingAddress.Country.Should().NotBeNullOrEmpty();
        orderDetails.BillingAddress.PostalCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetOrderDetails_AsCustomer_ForOwnOrder_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
    }

    [Fact]
    public async Task GetOrderDetails_AsAdmin_ForAnyOrder_ShouldReturnSuccess()
    {
        // Arrange
        // First create an order as customer
        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync();

        // Then authenticate as admin and retrieve order details
        var adminAccessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(adminAccessToken);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
    }

    [Fact]
    public async Task GetOrderDetails_WithDifferentOrderStatuses_ShouldReturnCorrectStatus()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var pendingOrderId = await CreateTestOrderWithStatusAsync(OrderStatus.Pending);
        var processingOrderId = await CreateTestOrderWithStatusAsync(OrderStatus.Processing);
        var shippedOrderId = await CreateTestOrderWithStatusAsync(OrderStatus.Shipped);

        // Act
        var pendingResponse = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{pendingOrderId}");
        var processingResponse = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{processingOrderId}");
        var shippedResponse = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{shippedOrderId}");

        // Assert
        AssertApiSuccess(pendingResponse);
        pendingResponse!.Data.Status.Should().Be(OrderStatus.Pending);

        AssertApiSuccess(processingResponse);
        processingResponse!.Data.Status.Should().Be(OrderStatus.Processing);

        AssertApiSuccess(shippedResponse);
        shippedResponse!.Data.Status.Should().Be(OrderStatus.Shipped);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetOrderDetails_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var orderId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"v1/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrderDetails_AsCustomer_ForOtherCustomersOrder_ShouldReturnForbidden()
    {
        // Arrange
        // First ensure customer user exists and create order for them
        await AuthenticateAsCustomerAsync();
        var customerOrderId = await CreateTestOrderForCustomerAsync();

        // Create and authenticate as a different customer
        await RegisterSecondCustomerAsync();
        var secondCustomerAccessToken = await AuthenticateAsync("customer2@shopilent.com", "Customer123!");
        SetAuthenticationHeader(secondCustomerAccessToken);

        // Act
        var response = await Client.GetAsync($"v1/orders/{customerOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not authorized", "forbidden", "AccessDenied");
    }

    [Fact]
    public async Task GetOrderDetails_AsManager_ForAnyOrder_ShouldReturnSuccess()
    {
        // Arrange
        // First create an order as customer
        await EnsureCustomerUserExistsAsync();
        var orderId = await CreateTestOrderForCustomerAsync();

        // Create and authenticate as manager
        await RegisterManagerAsync();
        var managerAccessToken = await AuthenticateAsync("manager@shopilent.com", "Manager123!");
        SetAuthenticationHeader(managerAccessToken);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(orderId);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetOrderDetails_WithNonExistentOrderId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var nonExistentOrderId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"v1/orders/{nonExistentOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist", "Order");
    }

    [Fact]
    public async Task GetOrderDetails_WithInvalidOrderId_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync("v1/orders/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrderDetails_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await Client.GetAsync($"v1/orders/{Guid.Empty}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Order ID", "cannot be empty", "required");
    }

    #endregion

    #region Order Details Content Tests

    [Fact]
    public async Task GetOrderDetails_WithMetadata_ShouldReturnMetadata()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var metadata = new Dictionary<string, object>
        {
            { "gift_message", "Happy Birthday!" },
            { "gift_wrapping", true },
            { "special_instructions", "Leave at front door" }
        };

        var orderId = await CreateTestOrderWithMetadataAsync(metadata);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.Metadata.Should().NotBeNull();
        orderDetails.Metadata.Should().ContainKey("gift_message");
        orderDetails.Metadata.Should().ContainKey("gift_wrapping");
        orderDetails.Metadata.Should().ContainKey("special_instructions");
    }

    [Fact]
    public async Task GetOrderDetails_WithoutMetadata_ShouldReturnEmptyOrNullMetadata()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        // Metadata can be null or empty
        if (orderDetails.Metadata != null)
        {
            orderDetails.Metadata.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GetOrderDetails_WithTrackingNumber_ShouldReturnTrackingNumber()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var trackingNumber = "TRACK123456789";
        var orderId = await CreateTestOrderWithTrackingNumberAsync(trackingNumber);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.TrackingNumber.Should().Be(trackingNumber);
    }

    [Fact]
    public async Task GetOrderDetails_CancelledOrder_ShouldShowCancelledStatus()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderWithStatusAsync(OrderStatus.Cancelled);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task GetOrderDetails_RefundedOrder_ShouldIncludeRefundInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var refundAmount = 99.99m;
        var refundReason = "Customer request";
        var orderId = await CreateTestOrderWithRefundAsync(refundAmount, refundReason);

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.RefundedAmount.Should().Be(orderDetails.Total);
        orderDetails.RefundReason.Should().Be(refundReason);
        orderDetails.RefundedAt.Should().NotBeNull();
        orderDetails.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetOrderDetails_CalledTwice_ShouldReturnConsistentData()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderAsync();

        // Act
        var firstResponse = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");
        var secondResponse = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(firstResponse);
        AssertApiSuccess(secondResponse);

        firstResponse!.Data.Id.Should().Be(secondResponse!.Data.Id);
        firstResponse.Data.Total.Should().Be(secondResponse.Data.Total);
        firstResponse.Data.Status.Should().Be(secondResponse.Data.Status);
        firstResponse.Data.Items.Count.Should().Be(secondResponse.Data.Items.Count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetOrderDetails_OrderWithVariants_ShouldReturnVariantInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderWithVariantsAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.Items.Should().NotBeEmpty();
        orderDetails.Items.Should().Contain(item => item.VariantId != null && item.VariantId != Guid.Empty);
    }

    [Fact]
    public async Task GetOrderDetails_OrderWithSameShippingAndBillingAddress_ShouldReturnSameAddress()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var orderId = await CreateTestOrderWithSameAddressAsync();

        // Act
        var response = await GetApiResponseAsync<OrderDetailDto>($"v1/orders/{orderId}");

        // Assert
        AssertApiSuccess(response);
        var orderDetails = response!.Data;

        orderDetails.ShippingAddressId.Should().Be(orderDetails.BillingAddressId);
        orderDetails.ShippingAddress.Id.Should().Be(orderDetails.BillingAddress.Id);
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
            Email = "customer2@shopilent.com", Password = "Customer123!", FirstName = "Customer2", LastName = "User"
        };

        await PostAsync("v1/auth/register", registerRequest);
        // Ignore if user already exists (409 Conflict) - that's expected after first test
    }

    /// <summary>
    /// Registers a manager user for testing manager access scenarios
    /// </summary>
    private async Task RegisterManagerAsync()
    {
        // The base class already has EnsureManagerUserExistsAsync method, so we can just call it
        await EnsureManagerUserExistsAsync();
    }

    /// <summary>
    /// Creates a basic test order for the currently authenticated customer user
    /// </summary>
    private async Task<Guid> CreateTestOrderAsync()
    {
        return await CreateTestOrderWithStatusAsync(OrderStatus.Pending);
    }

    /// <summary>
    /// Creates a test order with multiple items
    /// </summary>
    private async Task<Guid> CreateTestOrderWithMultipleItemsAsync()
    {
        var orderId = await ExecuteDbContextAsync(async context =>
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
                user: user,
                shippingAddress: address,
                billingAddress: address,
                subtotal: Money.Create(99.97m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            order.AddItem(product1, 1, Money.Create(29.99m, "USD").Value);
            order.AddItem(product2, 1, Money.Create(49.99m, "USD").Value);
            order.AddItem(product3, 1, Money.Create(19.99m, "USD").Value);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with specific status
    /// </summary>
    private async Task<Guid> CreateTestOrderWithStatusAsync(OrderStatus status)
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, "Test Product", 99.99m);

            var order = Order.Create(
                user: user,
                shippingAddress: address,
                billingAddress: address,
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

        await UpdateOrderStatusAsync(orderId, status);
        return orderId;
    }

    /// <summary>
    /// Creates a test order for the default customer (not the currently authenticated user)
    /// </summary>
    private async Task<Guid> CreateTestOrderForCustomerAsync()
    {
        return await CreateTestOrderWithStatusAsync(OrderStatus.Pending);
    }

    /// <summary>
    /// Creates a test order with metadata
    /// </summary>
    private async Task<Guid> CreateTestOrderWithMetadataAsync(Dictionary<string, object> metadata)
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, "Test Product", 99.99m);

            var order = Order.Create(
                user: user,
                shippingAddress: address,
                billingAddress: address,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            // Add metadata after order creation
            foreach (var kvp in metadata)
            {
                order.UpdateMetadata(kvp.Key, kvp.Value);
            }

            order.AddItem(product, 1, Money.Create(99.99m, "USD").Value);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with tracking number
    /// </summary>
    private async Task<Guid> CreateTestOrderWithTrackingNumberAsync(string trackingNumber)
    {
        // Create as Processing (paid but not shipped yet)
        var orderId = await CreateTestOrderWithStatusAsync(OrderStatus.Processing);

        // Then mark as shipped with tracking number
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.MarkAsShipped(trackingNumber);

                // Explicitly mark the order as modified to ensure EF Core tracks metadata changes
                context.Entry(order).State = EntityState.Modified;
                context.Entry(order).Property(o => o.Metadata).IsModified = true;

                await context.SaveChangesAsync();
            }
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with refund information
    /// </summary>
    private async Task<Guid> CreateTestOrderWithRefundAsync(decimal refundAmount, string refundReason)
    {
        var orderId = await CreateTestOrderWithStatusAsync(OrderStatus.Processing);

        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.MarkAsPaid();
                await context.SaveChangesAsync();

                order.ProcessRefund(refundReason);
                await context.SaveChangesAsync();
            }
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with product variants
    /// </summary>
    private async Task<Guid> CreateTestOrderWithVariantsAsync()
    {
        var orderId = await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
                throw new InvalidOperationException("Customer user not found.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, "Test Product with Variants", 99.99m);

            // Create a variant
            var variant = ProductVariant.Create(
                productId: product.Id,
                sku: $"SKU-VAR-{Guid.NewGuid():N}",
                price: Money.Create(99.99m, "USD").Value,
                stockQuantity: 10
            ).Value;

            product.AddVariant(variant);

            var order = Order.Create(
                user: user,
                shippingAddress: address,
                billingAddress: address,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            order.AddItem(product, 1, Money.Create(99.99m, "USD").Value, variant);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });

        return orderId;
    }

    /// <summary>
    /// Creates a test order with same shipping and billing address
    /// </summary>
    private async Task<Guid> CreateTestOrderWithSameAddressAsync()
    {
        return await CreateTestOrderAsync(); // Default implementation uses same address
    }

    /// <summary>
    /// Updates the status of an order
    /// </summary>
    private async Task UpdateOrderStatusAsync(Guid orderId, OrderStatus targetStatus)
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
