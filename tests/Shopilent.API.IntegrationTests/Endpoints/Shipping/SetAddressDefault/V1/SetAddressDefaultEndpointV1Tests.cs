using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.Endpoints.Shipping.SetAddressDefault.V1;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.Application.Features.Shipping.Commands.CreateAddress.V1;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.API.IntegrationTests.Endpoints.Shipping.SetAddressDefault.V1;

public class SetAddressDefaultEndpointV1Tests : ApiIntegrationTestBase
{
    public SetAddressDefaultEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task SetAddressDefault_WithValidAddressId_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address first
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act - Set address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(addressId);
        response.Data.IsDefault.Should().BeTrue();
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SetAddressDefault_WithValidAddressId_ShouldUpdateDatabaseCorrectly()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act - Set address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);

        // Verify in database
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);

            address.Should().NotBeNull();
            address!.IsDefault.Should().BeTrue();
            address.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task SetAddressDefault_WhenAddressAlreadyDefault_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create a default address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: true);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act - Set same address as default again
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetAddressDefault_ShouldUnsetOtherDefaultAddressOfSameType()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create first default shipping address
        var firstRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: true);
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        // Create second shipping address (non-default)
        var secondRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: false);
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);
        AssertApiSuccess(secondResponse);
        var secondAddressId = secondResponse!.Data.Id;

        // Act - Set second address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{secondAddressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Id.Should().Be(secondAddressId);
        response.Data.IsDefault.Should().BeTrue();

        // Verify first address is no longer default
        await ExecuteDbContextAsync(async context =>
        {
            var firstAddr = await context.Addresses.FirstOrDefaultAsync(a => a.Id == firstAddressId);
            var secondAddr = await context.Addresses.FirstOrDefaultAsync(a => a.Id == secondAddressId);

            firstAddr.Should().NotBeNull();
            firstAddr!.IsDefault.Should().BeFalse();

            secondAddr.Should().NotBeNull();
            secondAddr!.IsDefault.Should().BeTrue();
        });
    }

    [Fact]
    public async Task SetAddressDefault_WithDifferentAddressType_ShouldUnsetPreviousDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create default billing address
        var billingRequest = AddressTestDataV1.Creation.CreateBillingAddressRequest(isDefault: true);
        var billingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", billingRequest);
        AssertApiSuccess(billingResponse);
        var billingAddressId = billingResponse!.Data.Id;

        // Create shipping address
        var shippingRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: false);
        var shippingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", shippingRequest);
        AssertApiSuccess(shippingResponse);
        var shippingAddressId = shippingResponse!.Data.Id;

        // Act - Set shipping address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{shippingAddressId}/default", new { });

        // Assert
        AssertApiSuccess(response);

        // Verify billing address is no longer default (single default per user, regardless of type)
        await ExecuteDbContextAsync(async context =>
        {
            var billing = await context.Addresses.FirstOrDefaultAsync(a => a.Id == billingAddressId);
            var shipping = await context.Addresses.FirstOrDefaultAsync(a => a.Id == shippingAddressId);

            billing.Should().NotBeNull();
            billing!.IsDefault.Should().BeFalse();

            shipping.Should().NotBeNull();
            shipping!.IsDefault.Should().BeTrue();
        });
    }

    #endregion

    #region Address Type Tests

    [Theory]
    [InlineData(AddressType.Shipping)]
    [InlineData(AddressType.Billing)]
    [InlineData(AddressType.Both)]
    public async Task SetAddressDefault_WithAllAddressTypes_ShouldReturnSuccess(AddressType addressType)
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create address with specific type
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(addressType: addressType, isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsDefault.Should().BeTrue();
        response.Data.AddressType.Should().Be(addressType);
    }

    [Fact]
    public async Task SetAddressDefault_WithBothTypeAddress_ShouldUnsetBothShippingAndBillingDefaults()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create default shipping address
        var shippingRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: true);
        var shippingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", shippingRequest);
        AssertApiSuccess(shippingResponse);
        var shippingAddressId = shippingResponse!.Data.Id;

        // Create default billing address
        var billingRequest = AddressTestDataV1.Creation.CreateBillingAddressRequest(isDefault: true);
        var billingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", billingRequest);
        AssertApiSuccess(billingResponse);
        var billingAddressId = billingResponse!.Data.Id;

        // Create Both type address
        var bothRequest = AddressTestDataV1.Creation.CreateBothAddressRequest(isDefault: false);
        var bothResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", bothRequest);
        AssertApiSuccess(bothResponse);
        var bothAddressId = bothResponse!.Data.Id;

        // Act - Set Both type address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{bothAddressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsDefault.Should().BeTrue();
        response.Data.AddressType.Should().Be(AddressType.Both);

        // Verify both shipping and billing defaults were unset
        await ExecuteDbContextAsync(async context =>
        {
            var shipping = await context.Addresses.FirstOrDefaultAsync(a => a.Id == shippingAddressId);
            var billing = await context.Addresses.FirstOrDefaultAsync(a => a.Id == billingAddressId);
            var both = await context.Addresses.FirstOrDefaultAsync(a => a.Id == bothAddressId);

            shipping.Should().NotBeNull();
            billing.Should().NotBeNull();
            both.Should().NotBeNull();
            both!.IsDefault.Should().BeTrue();
        });
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task SetAddressDefault_WithEmptyGuid_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var emptyGuid = Guid.Empty;

        // Act
        var response = await PutAsync($"v1/addresses/{emptyGuid}/default", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address ID is required");
    }

    [Fact]
    public async Task SetAddressDefault_WithNonExistentAddress_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await PutAsync($"v1/addresses/{nonExistentId}/default", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "NotFound");
    }

    [Fact]
    public async Task SetAddressDefault_WithInvalidGuidFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await PutAsync("v1/addresses/invalid-guid/default", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task SetAddressDefault_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var addressId = Guid.NewGuid();

        // Act
        var response = await PutAsync($"v1/addresses/{addressId}/default", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetAddressDefault_WithAuthenticatedUser_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task SetAddressDefault_WithAdminRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // Create address for admin
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task SetAddressDefault_WithAnotherUsersAddress_ShouldReturnForbidden()
    {
        // Arrange - Create address with first customer
        var firstUserToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(firstUserToken);

        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Create and authenticate as manager user (different user)
        var secondUserToken = await AuthenticateAsManagerAsync();
        SetAuthenticationHeader(secondUserToken);

        // Act - Try to set first user's address as default
        var response = await PutAsync($"v1/addresses/{addressId}/default", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not owned", "own addresses", "Forbidden");
    }

    #endregion

    #region Multiple Addresses Tests

    [Fact]
    public async Task SetAddressDefault_WithMultipleAddressesSameType_ShouldOnlyOneBeDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple shipping addresses
        var addressIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var createRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: i == 0);
            var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
            AssertApiSuccess(createResponse);
            addressIds.Add(createResponse!.Data.Id);
        }

        // Act - Set the last address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressIds[2]}/default", new { });

        // Assert
        AssertApiSuccess(response);

        // Verify only one address is default
        await ExecuteDbContextAsync(async context =>
        {
            var addresses = await context.Addresses
                .Where(a => addressIds.Contains(a.Id))
                .ToListAsync();

            addresses.Should().HaveCount(3);
            addresses.Count(a => a.IsDefault).Should().Be(1);
            addresses.Single(a => a.IsDefault).Id.Should().Be(addressIds[2]);
        });
    }

    [Fact]
    public async Task SetAddressDefault_WithMixedAddressTypes_ShouldHaveSingleDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create default shipping address
        var shippingRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest(isDefault: true);
        var shippingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", shippingRequest);
        AssertApiSuccess(shippingResponse);
        var shippingAddressId = shippingResponse!.Data.Id;

        // Create non-default billing address
        var billingRequest = AddressTestDataV1.Creation.CreateBillingAddressRequest(isDefault: false);
        var billingResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", billingRequest);
        AssertApiSuccess(billingResponse);
        var billingAddressId = billingResponse!.Data.Id;

        // Act - Set billing address as default
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{billingAddressId}/default", new { });

        // Assert
        AssertApiSuccess(response);

        // Verify only the new address is default (single default per user)
        await ExecuteDbContextAsync(async context =>
        {
            var shipping = await context.Addresses.FirstOrDefaultAsync(a => a.Id == shippingAddressId);
            var billing = await context.Addresses.FirstOrDefaultAsync(a => a.Id == billingAddressId);

            shipping.Should().NotBeNull();
            shipping!.IsDefault.Should().BeFalse();

            billing.Should().NotBeNull();
            billing!.IsDefault.Should().BeTrue();
        });
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task SetAddressDefault_MultipleConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple addresses
        var addressIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var createRequest = AddressTestDataV1.Creation.CreateValidRequest(isDefault: false);
            var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
            AssertApiSuccess(createResponse);
            addressIds.Add(createResponse!.Data.Id);
        }

        // Process outbox messages to ensure initial state is clean
        await ProcessOutboxMessagesAsync();

        // Act - Set addresses as default concurrently (they will compete for the single default slot)
        var tasks = addressIds
            .Select(id => PutAsync($"v1/addresses/{id}/default", new { }))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Process outbox messages after concurrent operations
        await ProcessOutboxMessagesAsync();

        // Assert - At least one request should succeed, others may fail due to concurrency
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        successCount.Should().BeGreaterOrEqualTo(1, "at least one request should succeed");

        // Verify exactly one address is default (single default per user)
        await ExecuteDbContextAsync(async context =>
        {
            var addresses = await context.Addresses
                .Where(a => addressIds.Contains(a.Id))
                .ToListAsync();

            addresses.Should().HaveCount(3);
            addresses.Count(a => a.IsDefault).Should().Be(1, "exactly one address should be default");
        });
    }

    #endregion

    #region Response Content Tests

    [Fact]
    public async Task SetAddressDefault_ShouldReturnCompleteAddressInformation()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(
            addressLine1: "123 Test Street",
            city: "Test City",
            state: "Test State",
            postalCode: "12345",
            country: "Test Country",
            phone: "+1234567890",
            addressType: AddressType.Shipping,
            isDefault: false);

        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await PutApiResponseAsync<object, SetAddressDefaultResponseV1>($"v1/addresses/{addressId}/default", new { });

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().Be(addressId);
        response.Data.AddressLine1.Should().Be("123 Test Street");
        response.Data.City.Should().Be("Test City");
        response.Data.State.Should().Be("Test State");
        response.Data.PostalCode.Should().Be("12345");
        response.Data.Country.Should().Be("Test Country");
        response.Data.Phone.Should().Be("+1234567890");
        response.Data.AddressType.Should().Be(AddressType.Shipping);
        response.Data.IsDefault.Should().BeTrue();
        response.Data.UserId.Should().NotBeEmpty();
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        response.Data.UpdatedAt.Should().BeAfter(response.Data.CreatedAt);
    }

    #endregion
}
