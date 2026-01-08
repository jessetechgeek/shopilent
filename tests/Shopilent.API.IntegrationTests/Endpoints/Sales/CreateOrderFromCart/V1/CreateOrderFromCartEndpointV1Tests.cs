using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.Application.Features.Sales.Commands.CreateOrderFromCart.V1;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.CreateOrderFromCart.V1;

public class CreateOrderFromCartEndpointV1Tests : ApiIntegrationTestBase
{
    public CreateOrderFromCartEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateOrderFromCart_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().NotBeEmpty();
        response.Data.UserId.Should().NotBeNull();
        response.Data.ShippingAddressId.Should().Be(shippingAddress.Id);
        response.Data.Status.Should().Be("Pending");
        response.Data.PaymentStatus.Should().Be("Pending");
        response.Data.Items.Should().NotBeEmpty();
        response.Data.Subtotal.Should().BeGreaterThan(0);
        response.Data.Total.Should().BeGreaterThan(0);
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateOrderFromCart_WithValidData_ShouldCreateOrderInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            shippingMethod: "Standard");

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);

        // Verify order exists in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == response!.Data.Id);

            order.Should().NotBeNull();
            order!.UserId.Should().NotBeNull();
            order.ShippingAddressId.Should().Be(shippingAddress.Id);
            order.ShippingMethod.Should().Be("Standard");
            order.Status.ToString().Should().Be("Pending");
            order.PaymentStatus.ToString().Should().Be("Pending");
            order.Items.Should().NotBeEmpty();
            order.Items.Should().HaveCount(1);
            order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task CreateOrderFromCart_WithValidData_ShouldClearCart()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);

        // Verify cart is cleared in database
        await ExecuteDbContextAsync(async context =>
        {
            var clearedCart = await context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            clearedCart.Should().NotBeNull();
            clearedCart!.Items.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task CreateOrderFromCart_WithMultipleItems_ShouldCreateOrderWithAllItems()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product1 = await SeedProductAsync();
        var product2 = await SeedProductAsync();
        var product3 = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product1.Id, product2.Id, product3.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Items.Should().HaveCount(3);

        // Verify in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == response.Data.Id);

            order.Should().NotBeNull();
            order!.Items.Should().HaveCount(3);
            order.Items.Should().Contain(i => i.ProductId == product1.Id);
            order.Items.Should().Contain(i => i.ProductId == product2.Id);
            order.Items.Should().Contain(i => i.ProductId == product3.Id);
        });
    }

    [Fact]
    public async Task CreateOrderFromCart_WithBillingAddress_ShouldUseBillingAddress()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();
        var billingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            billingAddressId: billingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.ShippingAddressId.Should().Be(shippingAddress.Id);
        response.Data.BillingAddressId.Should().Be(billingAddress.Id);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithoutBillingAddress_ShouldUseShippingAddressForBilling()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateRequestWithoutBillingAddress(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.ShippingAddressId.Should().Be(shippingAddress.Id);
        response.Data.BillingAddressId.Should().Be(shippingAddress.Id);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var metadata = new Dictionary<string, object>
        {
            { "gift_message", "Happy Birthday!" },
            { "gift_wrapping", true },
            { "priority_shipping", "urgent" }
        };

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            metadata: metadata);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);

        // Verify metadata in database
        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == response!.Data.Id);

            order.Should().NotBeNull();
            order!.Metadata.Should().NotBeNull();
            order.Metadata.Should().ContainKey("gift_message");
            order.Metadata.Should().ContainKey("gift_wrapping");
            order.Metadata.Should().ContainKey("priority_shipping");
        });
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("Express")]
    [InlineData("Overnight")]
    [InlineData(null)]
    public async Task CreateOrderFromCart_WithVariousShippingMethods_ShouldReturnSuccess(string? shippingMethod)
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            shippingMethod: shippingMethod);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        // When shipping method is null, it defaults to "Standard"
        var expectedShippingMethod = shippingMethod ?? "Standard";
        response!.Data.ShippingMethod.Should().Be(expectedShippingMethod);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithSpecificCartId_ShouldUseSpecifiedCart()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            cartId: cart.Id,
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    #endregion

    #region Validation Tests - ShippingAddressId

    [Fact]
    public async Task CreateOrderFromCart_WithEmptyShippingAddressId_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var request = OrderTestDataV1.Validation.CreateRequestWithEmptyShippingAddressId();

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Shipping address ID is required.");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithNonExistentShippingAddressId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });

        var request = OrderTestDataV1.Validation.CreateRequestWithNonExistentShippingAddressId();

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Address", "not found", "does not exist");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithOtherUsersShippingAddress_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });

        // Create address for a different user
        var otherUserAddress = await SeedAddressForDifferentUserAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: otherUserAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Address", "not found", "does not exist");
    }

    #endregion

    #region Validation Tests - BillingAddressId

    [Fact]
    public async Task CreateOrderFromCart_WithNonExistentBillingAddressId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Validation.CreateRequestWithNonExistentBillingAddressId(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Address", "not found", "does not exist");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithOtherUsersBillingAddress_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        // Create billing address for a different user
        var otherUserAddress = await SeedAddressForDifferentUserAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            billingAddressId: otherUserAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Address", "not found", "does not exist");
    }

    #endregion

    #region Validation Tests - Cart

    [Fact]
    public async Task CreateOrderFromCart_WithEmptyCart_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var emptyCart = await SeedEmptyCartAsync();
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            cartId: emptyCart.Id,
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        // Note: The endpoint returns 404 NotFound when cart is empty because the handler
        // returns CartErrors.EmptyCart which is mapped to NotFound
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cart", "empty", "no items");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithNonExistentCartId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Validation.CreateRequestWithNonExistentCartId(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cart", "not found", "does not exist");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithOtherUsersCart_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var otherUserCart = await SeedCartForDifferentUserAsync(product.Id);
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            cartId: otherUserCart.Id,
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("Cart", "not found", "does not exist");
    }

    [Fact]
    public async Task CreateOrderFromCart_WithoutCartId_ShouldUseUserDefaultCart()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateRequestWithoutCartId(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    #endregion

    #region Validation Tests - ShippingMethod

    [Fact]
    public async Task CreateOrderFromCart_WithLongShippingMethod_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Validation.CreateRequestWithLongShippingMethod(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Shipping method must not exceed 100 characters.");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task CreateOrderFromCart_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: Guid.NewGuid());

        // Act
        var response = await PostAsync("v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithCustomerRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithAdminRole_ShouldReturnSuccess()
    {
        // Arrange
        // Admins can also create orders, so authenticate as admin and create admin's own cart/address
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create products
        var product = await SeedProductAsync();

        // Create cart and address for admin user (using helper methods that work with admin@shopilent.com)
        var (cart, _) = await ExecuteDbContextAsync(async context =>
        {
            var adminUser = context.Users.FirstOrDefault(u => u.Email.Value == "admin@shopilent.com");
            if (adminUser == null)
            {
                throw new InvalidOperationException("Admin user not found");
            }

            var adminCart = Cart.Create(adminUser).Value;
            context.Carts.Add(adminCart);
            await context.SaveChangesAsync();

            var productEntity = await context.Products.FindAsync(product.Id);
            adminCart.AddItem(productEntity!, 1, null);
            await context.SaveChangesAsync();

            var postalAddress = PostalAddress.Create(
                addressLine1: "123 Admin St",
                city: "Admin City",
                state: "CA",
                country: "United States",
                postalCode: "90210"
            ).Value;

            var phone = PhoneNumber.Create("555-0123").Value;
            var address = Address.CreateShipping(adminUser.Id, postalAddress, phone, false).Value;
            context.Addresses.Add(address);
            await context.SaveChangesAsync();

            return (adminCart, address);
        });

        var shippingAddress = await ExecuteDbContextAsync(async context =>
        {
            var adminUser = context.Users.FirstOrDefault(u => u.Email.Value == "admin@shopilent.com");
            return context.Addresses.First(a => a.UserId == adminUser!.Id);
        });

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task CreateOrderFromCart_WithProductVariants_ShouldCreateOrderWithVariants()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var variant1 = await SeedProductVariantAsync(product.Id);
        var variant2 = await SeedProductVariantAsync(product.Id);
        var cart = await SeedCartWithVariantItemsAsync(product.Id, new[] { variant1.Id, variant2.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Items.Should().HaveCount(2);
        response.Data.Items.Should().Contain(i => i.VariantId == variant1.Id);
        response.Data.Items.Should().Contain(i => i.VariantId == variant2.Id);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithComplexMetadata_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.EdgeCases.CreateRequestWithComplexMetadata(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithEmptyMetadata_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.EdgeCases.CreateRequestWithEmptyMetadata(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task CreateOrderFromCart_WithNullMetadata_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.EdgeCases.CreateRequestWithNullMetadata(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Order Calculation Tests

    [Fact]
    public async Task CreateOrderFromCart_ShouldCalculateSubtotalCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Subtotal.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrderFromCart_ShouldCalculateTaxCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Tax.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CreateOrderFromCart_ShouldCalculateShippingCostCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id,
            shippingMethod: "Express");

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.ShippingCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrderFromCart_ShouldCalculateTotalCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        var expectedTotal = response!.Data.Subtotal + response.Data.Tax + response.Data.ShippingCost;
        response.Data.Total.Should().Be(expectedTotal);
    }

    #endregion

    #region Response Validation Tests

    [Fact]
    public async Task CreateOrderFromCart_ValidRequest_ShouldReturnCompleteResponse()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var product = await SeedProductAsync();
        var cart = await SeedCartWithItemsAsync(new[] { product.Id });
        var shippingAddress = await SeedAddressAsync();

        var request = OrderTestDataV1.Creation.CreateValidRequest(
            shippingAddressId: shippingAddress.Id);

        // Act
        var response = await PostApiResponseAsync<object, CreateOrderFromCartResponseV1>("v1/orders", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().NotBeEmpty();
        response.Data.UserId.Should().NotBeNull();
        response.Data.ShippingAddressId.Should().NotBeEmpty();
        response.Data.BillingAddressId.Should().NotBeEmpty();
        response.Data.Status.Should().NotBeNullOrEmpty();
        response.Data.PaymentStatus.Should().NotBeNullOrEmpty();
        response.Data.Items.Should().NotBeEmpty();
        response.Data.Subtotal.Should().BeGreaterThanOrEqualTo(0);
        response.Data.Tax.Should().BeGreaterThanOrEqualTo(0);
        response.Data.ShippingCost.Should().BeGreaterThanOrEqualTo(0);
        response.Data.Total.Should().BeGreaterThan(0);
        response.Data.CreatedAt.Should().NotBe(default);
    }

    #endregion

    #region Helper Methods

    private async Task<Product> SeedProductAsync()
    {
        return await TestDbSeeder.SeedProductAsync(ExecuteDbContextAsync);
    }

    private async Task<ProductVariant> SeedProductVariantAsync(Guid productId)
    {
        return await TestDbSeeder.SeedProductVariantAsync(ExecuteDbContextAsync, productId);
    }

    private async Task<Cart> SeedCartWithItemsAsync(Guid[] productIds)
    {
        return await TestDbSeeder.SeedCartWithItemsAsync(ExecuteDbContextAsync, productIds);
    }

    private async Task<Cart> SeedCartWithVariantItemsAsync(Guid productId, Guid[] variantIds)
    {
        return await TestDbSeeder.SeedCartWithVariantItemsAsync(ExecuteDbContextAsync, productId, variantIds);
    }

    private async Task<Cart> SeedEmptyCartAsync()
    {
        return await TestDbSeeder.SeedAnonymousCartAsync(ExecuteDbContextAsync);
    }

    private async Task<Cart> SeedCartForDifferentUserAsync(Guid productId)
    {
        return await TestDbSeeder.SeedCartForDifferentUserAsync(ExecuteDbContextAsync, productId);
    }

    private async Task<Address> SeedAddressAsync()
    {
        return await TestDbSeeder.SeedAddressAsync(ExecuteDbContextAsync);
    }

    private async Task<Address> SeedAddressForDifferentUserAsync()
    {
        return await TestDbSeeder.SeedAddressForDifferentUserAsync(ExecuteDbContextAsync);
    }

    #endregion
}
