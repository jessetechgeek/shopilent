using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.Application.Features.Shipping.Commands.CreateAddress.V1;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.API.IntegrationTests.Endpoints.Shipping.CreateAddress.V1;

public class CreateAddressEndpointV1Tests : ApiIntegrationTestBase
{
    public CreateAddressEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateAddress_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Id.Should().NotBeEmpty();
        response.Data.UserId.Should().NotBeEmpty();
        response.Data.AddressLine1.Should().NotBeNullOrEmpty();
        response.Data.City.Should().NotBeNullOrEmpty();
        response.Data.State.Should().NotBeNullOrEmpty();
        response.Data.PostalCode.Should().NotBeNullOrEmpty();
        response.Data.Country.Should().NotBeNullOrEmpty();
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateAddress_WithValidData_ShouldCreateAddressInDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest(
            addressLine1: "123 Test Street",
            city: "Test City",
            state: "Test State",
            postalCode: "12345",
            country: "Test Country");

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);

        // Verify address exists in database
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == response!.Data.Id);

            address.Should().NotBeNull();
            address!.AddressLine1.Should().Be("123 Test Street");
            address.City.Should().Be("Test City");
            address.State.Should().Be("Test State");
            address.PostalCode.Should().Be("12345");
            address.Country.Should().Be("Test Country");
            address.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task CreateAddress_WithShippingType_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateShippingAddressRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressType.Should().Be(AddressType.Shipping);
    }

    [Fact]
    public async Task CreateAddress_WithBillingType_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateBillingAddressRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressType.Should().Be(AddressType.Billing);
    }

    [Fact]
    public async Task CreateAddress_WithBothType_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateBothAddressRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressType.Should().Be(AddressType.Both);
    }

    [Theory]
    [InlineData(AddressType.Shipping)]
    [InlineData(AddressType.Billing)]
    [InlineData(AddressType.Both)]
    public async Task CreateAddress_WithAllValidAddressTypes_ShouldReturnSuccess(AddressType addressType)
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest(addressType: addressType);

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressType.Should().Be(addressType);
    }

    [Fact]
    public async Task CreateAddress_WithoutPhone_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateAddressWithoutPhone();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Phone.Should().BeNull();
    }

    [Fact]
    public async Task CreateAddress_WithoutAddressLine2_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateAddressWithoutLine2();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine2.Should().BeNull();
    }

    #endregion

    #region Default Address Management Tests

    [Fact]
    public async Task CreateAddress_WithIsDefaultTrue_ShouldSetAsDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAddress_WithIsDefaultTrue_ShouldUnsetPreviousDefaultOfSameType()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create first default shipping address
        var firstRequest = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        // Create second default shipping address
        var secondRequest = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);

        // Assert
        AssertApiSuccess(secondResponse);
        secondResponse!.Data.IsDefault.Should().BeTrue();

        // Verify first address is no longer default
        await ExecuteDbContextAsync(async context =>
        {
            var firstAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == firstAddressId);
            firstAddress.Should().NotBeNull();
            firstAddress!.IsDefault.Should().BeFalse();

            var secondAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == secondResponse.Data.Id);
            secondAddress.Should().NotBeNull();
            secondAddress!.IsDefault.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CreateAddress_WithNewDefault_ShouldUnsetPreviousDefault()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create first default address
        var firstRequest = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        // Create second default address (different type, but should still unset the first)
        var secondRequest = AddressTestDataV1.DefaultManagement.CreateDefaultBillingAddress();
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);

        // Assert
        AssertApiSuccess(secondResponse);

        // Verify only the new address is default (single default per user, regardless of type)
        await ExecuteDbContextAsync(async context =>
        {
            var firstAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == firstAddressId);
            firstAddress.Should().NotBeNull();
            firstAddress!.IsDefault.Should().BeFalse();

            var secondAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == secondResponse!.Data.Id);
            secondAddress.Should().NotBeNull();
            secondAddress!.IsDefault.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CreateAddress_WithIsDefaultFalse_ShouldNotAffectOtherAddresses()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create first address as default
        var firstRequest = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        // Create second address as non-default
        var secondRequest = AddressTestDataV1.DefaultManagement.CreateNonDefaultAddress();
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);

        // Assert
        AssertApiSuccess(secondResponse);
        secondResponse!.Data.IsDefault.Should().BeFalse();

        // Verify first address is still default
        await ExecuteDbContextAsync(async context =>
        {
            var firstAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == firstAddressId);
            firstAddress.Should().NotBeNull();
            firstAddress!.IsDefault.Should().BeTrue();
        });
    }

    #endregion

    #region Validation Tests - AddressLine1

    [Fact]
    public async Task CreateAddress_WithEmptyAddressLine1_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithEmptyAddressLine1();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address line 1 is required.");
    }

    [Fact]
    public async Task CreateAddress_WithNullAddressLine1_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithNullAddressLine1();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address line 1 is required.");
    }

    [Fact]
    public async Task CreateAddress_WithWhitespaceAddressLine1_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithWhitespaceAddressLine1();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address line 1 is required.");
    }

    [Fact]
    public async Task CreateAddress_WithLongAddressLine1_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongAddressLine1();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address line 1 must not exceed 255 characters.");
    }

    #endregion

    #region Validation Tests - AddressLine2

    [Fact]
    public async Task CreateAddress_WithLongAddressLine2_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongAddressLine2();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address line 2 must not exceed 255 characters.");
    }

    #endregion

    #region Validation Tests - City

    [Fact]
    public async Task CreateAddress_WithEmptyCity_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithEmptyCity();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("City is required.");
    }

    [Fact]
    public async Task CreateAddress_WithNullCity_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithNullCity();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("City is required.");
    }

    [Fact]
    public async Task CreateAddress_WithLongCity_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongCity();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("City must not exceed 100 characters.");
    }

    #endregion

    #region Validation Tests - State

    [Fact]
    public async Task CreateAddress_WithEmptyState_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithEmptyState();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("State is required.");
    }

    [Fact]
    public async Task CreateAddress_WithNullState_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithNullState();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("State is required.");
    }

    [Fact]
    public async Task CreateAddress_WithLongState_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongState();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("State must not exceed 100 characters.");
    }

    #endregion

    #region Validation Tests - PostalCode

    [Fact]
    public async Task CreateAddress_WithEmptyPostalCode_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithEmptyPostalCode();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Postal code is required.");
    }

    [Fact]
    public async Task CreateAddress_WithNullPostalCode_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithNullPostalCode();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Postal code is required.");
    }

    [Fact]
    public async Task CreateAddress_WithLongPostalCode_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongPostalCode();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Postal code must not exceed 20 characters.");
    }

    #endregion

    #region Validation Tests - Country

    [Fact]
    public async Task CreateAddress_WithEmptyCountry_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithEmptyCountry();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Country is required.");
    }

    [Fact]
    public async Task CreateAddress_WithNullCountry_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithNullCountry();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Country is required.");
    }

    [Fact]
    public async Task CreateAddress_WithLongCountry_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongCountry();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Country must not exceed 100 characters.");
    }

    #endregion

    #region Validation Tests - Phone

    [Fact]
    public async Task CreateAddress_WithLongPhone_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithLongPhone();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Phone number must not exceed 20 characters.");
    }

    #endregion

    #region Validation Tests - AddressType

    [Fact]
    public async Task CreateAddress_WithInvalidAddressType_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Validation.CreateRequestWithInvalidAddressType();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Invalid address type.");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task CreateAddress_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var request = AddressTestDataV1.Creation.CreateValidRequest();

        // Act
        var response = await PostAsync("v1/addresses", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAddress_WithCustomerRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
    }

    [Fact]
    public async Task CreateAddress_WithAdminRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public async Task CreateAddress_WithMaximumLengthAddressLine1_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthAddressLine1();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine1.Length.Should().Be(255);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthAddressLine2_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthAddressLine2();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine2!.Length.Should().Be(255);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthCity_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthCity();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.City.Length.Should().Be(100);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthState_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthState();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.State.Length.Should().Be(100);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthPostalCode_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthPostalCode();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.PostalCode.Length.Should().Be(20);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthCountry_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthCountry();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Country.Length.Should().Be(100);
    }

    [Fact]
    public async Task CreateAddress_WithMaximumLengthPhone_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthPhone();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Phone!.Length.Should().Be(20);
    }

    [Fact]
    public async Task CreateAddress_WithMinimumValidAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.BoundaryTests.CreateRequestWithMinimumValidAddress();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine1.Should().Be("A");
        response.Data.City.Should().Be("B");
        response.Data.State.Should().Be("C");
        response.Data.PostalCode.Should().Be("1");
        response.Data.Country.Should().Be("D");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task CreateAddress_WithUnicodeCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.EdgeCases.CreateRequestWithUnicodeCharacters();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine1.Should().Contain("Café");
        response.Data.City.Should().Contain("São");
    }

    [Fact]
    public async Task CreateAddress_WithSpecialCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.EdgeCases.CreateRequestWithSpecialCharacters();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.AddressLine1.Should().Contain("#");
        response.Data.PostalCode.Should().Contain("-");
    }

    [Fact]
    public async Task CreateAddress_InternationalAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.EdgeCases.CreateInternationalAddress();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.City.Should().Be("Tokyo");
        response.Data.Country.Should().Be("Japan");
    }

    [Fact]
    public async Task CreateAddress_UKAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.EdgeCases.CreateUKAddress();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.City.Should().Be("London");
        response.Data.Country.Should().Be("United Kingdom");
        response.Data.PostalCode.Should().Contain(" "); // UK postcodes contain spaces
    }

    [Fact]
    public async Task CreateAddress_CanadianAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.EdgeCases.CreateCanadianAddress();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.City.Should().Be("Toronto");
        response.Data.Country.Should().Be("Canada");
    }

    #endregion

    #region Bulk/Performance Tests

    [Fact]
    public async Task CreateAddress_MultipleConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var tasks = Enumerable.Range(0, 5)
            .Select(i => AddressTestDataV1.Creation.CreateValidRequest(
                addressLine1: $"Address {i}, Main Street"))
            .Select(request => PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request))
            .ToList();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response => AssertApiSuccess(response));
        responses.Select(r => r!.Data.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateAddress_MultipleSequentialAddresses_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act & Assert - Create 3 addresses sequentially
        for (int i = 0; i < 3; i++)
        {
            var request = AddressTestDataV1.Creation.CreateValidRequest(
                addressLine1: $"Address {i + 1}, Test Street",
                isDefault: i == 0);

            var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);
            AssertApiSuccess(response);
            response!.Data.AddressLine1.Should().Contain($"Address {i + 1}");
        }

        // Verify all addresses exist in database
        await ExecuteDbContextAsync(async context =>
        {
            var addresses = await context.Addresses
                .Where(a => a.PostalAddress.AddressLine1.Contains("Test Street"))
                .ToListAsync();

            addresses.Should().HaveCountGreaterOrEqualTo(3);
        });
    }

    #endregion

    #region User Isolation Tests

    [Fact]
    public async Task CreateAddress_ShouldAssociateAddressWithCurrentUser()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = AddressTestDataV1.Creation.CreateValidRequest();

        // Act
        var response = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request);

        // Assert
        AssertApiSuccess(response);

        // Verify address is associated with the current user
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == response!.Data.Id);

            address.Should().NotBeNull();
            address!.UserId.Should().Be(response!.Data.UserId);
        });
    }

    #endregion
}
