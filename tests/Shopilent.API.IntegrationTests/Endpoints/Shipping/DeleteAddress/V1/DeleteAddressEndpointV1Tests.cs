using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.API.IntegrationTests.Common.TestData;
using Shopilent.API.Common.Models;
using Shopilent.Application.Features.Shipping.Commands.CreateAddress.V1;

namespace Shopilent.API.IntegrationTests.Endpoints.Shipping.DeleteAddress.V1;

public class DeleteAddressEndpointV1Tests : ApiIntegrationTestBase
{
    public DeleteAddressEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task DeleteAddress_WithValidId_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address first
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
        apiResponse!.Data.Should().Be("Address deleted successfully");
    }

    [Fact]
    public async Task DeleteAddress_WithValidId_ShouldRemoveAddressFromDatabase()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address first
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest(
            addressLine1: "123 Delete Test Street",
            city: "Delete City",
            state: "Delete State",
            postalCode: "99999");
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Verify address exists
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);
            address.Should().NotBeNull();
        });

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);

        // Verify address no longer exists in database
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);
            address.Should().BeNull();
        });
    }

    [Fact]
    public async Task DeleteAddress_ShippingAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.Creation.CreateShippingAddressRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_BillingAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.Creation.CreateBillingAddressRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_BothTypeAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.Creation.CreateBothAddressRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_DefaultAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.DefaultManagement.CreateDefaultShippingAddress();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_NonDefaultAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.DefaultManagement.CreateNonDefaultAddress();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task DeleteAddress_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await DeleteAsync($"v1/addresses/{Guid.Empty}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("Address ID is required.");
    }

    [Fact]
    public async Task DeleteAddress_WithInvalidGuidFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Act
        var response = await DeleteAsync("v1/addresses/invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAddress_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await DeleteAsync($"v1/addresses/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().ContainAny("not found", "does not exist");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task DeleteAddress_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var addressId = Guid.NewGuid();

        // Act
        var response = await DeleteAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAddress_WithCustomerRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAddress_WithAdminRole_ShouldReturnNotFoundForNonExistentAddress()
    {
        // Arrange - Test that admin has permission, but address doesn't exist
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await DeleteAsync($"v1/addresses/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Not forbidden, so admin has permission
    }

    #endregion

    #region User Isolation Tests

    [Fact]
    public async Task DeleteAddress_OwnedByDifferentUser_ShouldReturnNotFound()
    {
        // Arrange - Create address as first customer
        var firstUserToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(firstUserToken);

        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Create and authenticate as manager user (different user)
        var secondUserToken = await AuthenticateAsManagerAsync();
        SetAuthenticationHeader(secondUserToken);

        // Act - Try to delete first user's address as second user
        var response = await DeleteAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_UserCanOnlyDeleteOwnAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act - Delete own address
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    #endregion

    #region Conflict/Business Rule Tests

    [Fact]
    public async Task DeleteAddress_AlreadyDeleted_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Delete the address first time
        var firstDeleteResponse = await DeleteApiResponseAsync($"v1/addresses/{addressId}");
        firstDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstDeleteContent = await firstDeleteResponse.Content.ReadAsStringAsync();
        var firstDeleteApiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(firstDeleteContent, JsonOptions);
        AssertApiSuccess(firstDeleteApiResponse);

        // Act - Try to delete again
        var response = await DeleteAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_WhenMultipleAddressesExist_ShouldOnlyDeleteSpecified()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple addresses
        var firstRequest = AddressTestDataV1.Creation.CreateValidRequest(addressLine1: "First Address");
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        var secondRequest = AddressTestDataV1.Creation.CreateValidRequest(addressLine1: "Second Address");
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);
        AssertApiSuccess(secondResponse);
        var secondAddressId = secondResponse!.Data.Id;

        var thirdRequest = AddressTestDataV1.Creation.CreateValidRequest(addressLine1: "Third Address");
        var thirdResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", thirdRequest);
        AssertApiSuccess(thirdResponse);
        var thirdAddressId = thirdResponse!.Data.Id;

        // Act - Delete only the second address
        var response = await DeleteApiResponseAsync($"v1/addresses/{secondAddressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify first and third addresses still exist, second is deleted
        await ExecuteDbContextAsync(async context =>
        {
            var firstAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == firstAddressId);
            firstAddress.Should().NotBeNull();

            var secondAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == secondAddressId);
            secondAddress.Should().BeNull();

            var thirdAddress = await context.Addresses.FirstOrDefaultAsync(a => a.Id == thirdAddressId);
            thirdAddress.Should().NotBeNull();
        });
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task DeleteAddress_WithUnicodeCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.EdgeCases.CreateRequestWithUnicodeCharacters();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_WithSpecialCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.EdgeCases.CreateRequestWithSpecialCharacters();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_InternationalAddress_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.EdgeCases.CreateInternationalAddress();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    [Fact]
    public async Task DeleteAddress_WithMaximumLengthFields_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.BoundaryTests.CreateRequestWithMaximumLengthAddressLine1();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
    }

    #endregion

    #region Performance/Bulk Tests

    [Fact]
    public async Task DeleteAddress_MultipleConcurrentDeletes_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple addresses first (not default to avoid unique constraint violations)
        var createTasks = Enumerable.Range(0, 5)
            .Select(i => AddressTestDataV1.Creation.CreateValidRequest(
                addressLine1: $"Concurrent Address {i}",
                isDefault: false))
            .Select(request => PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", request))
            .ToList();

        var createResponses = await Task.WhenAll(createTasks);
        createResponses.Should().AllSatisfy(response => AssertApiSuccess(response));

        // Act - Delete all addresses concurrently
        var deleteTasks = createResponses
            .Select(response => DeleteApiResponseAsync($"v1/addresses/{response!.Data.Id}"))
            .ToList();

        var deleteResponses = await Task.WhenAll(deleteTasks);

        // Assert
        deleteResponses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task DeleteAddress_SequentialDeletes_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple addresses
        var addressIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var createRequest = AddressTestDataV1.Creation.CreateValidRequest(
                addressLine1: $"Sequential Address {i}");
            var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
            AssertApiSuccess(createResponse);
            addressIds.Add(createResponse!.Data.Id);
        }

        // Act & Assert - Delete addresses sequentially
        foreach (var addressId in addressIds)
        {
            var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
            AssertApiSuccess(apiResponse);
        }
    }

    #endregion

    #region Integration with Other Endpoints Tests

    [Fact]
    public async Task DeleteAddress_ThenAttemptToGetIt_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Verify address can be retrieved
        var getResponse = await Client.GetAsync($"v1/addresses/{addressId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify address exists in database before deletion
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);
            address.Should().NotBeNull("Address should exist before deletion");
        });

        // Act - Delete the address
        var deleteResponse = await DeleteApiResponseAsync($"v1/addresses/{addressId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
        var deleteApiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(deleteContent, JsonOptions);
        AssertApiSuccess(deleteApiResponse);

        // Verify address no longer exists in database after deletion
        await ExecuteDbContextAsync(async context =>
        {
            var address = await context.Addresses
                .FirstOrDefaultAsync(a => a.Id == addressId);
            address.Should().BeNull("Address should not exist after deletion");
        });

        // Assert - Verify address can no longer be retrieved via API
        var getAfterDeleteResponse = await Client.GetAsync($"v1/addresses/{addressId}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_ThenAttemptToUpdateIt_ShouldReturnNotFound()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create an address
        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Delete the address
        var deleteResponse = await DeleteApiResponseAsync($"v1/addresses/{addressId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
        var deleteApiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(deleteContent, JsonOptions);
        AssertApiSuccess(deleteApiResponse);

        // Act - Try to update the deleted address
        var updateRequest = new
        {
            AddressLine1 = "Updated Address",
            AddressLine2 = (string?)null,
            City = "Updated City",
            State = "Updated State",
            PostalCode = "99999",
            Country = "Updated Country",
            Phone = "+1234567890"
        };
        var updateResponse = await PutAsync($"v1/addresses/{addressId}", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_ThenCheckUserAddressList_ShouldNotContainDeletedAddress()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        // Create multiple addresses
        var firstRequest = AddressTestDataV1.Creation.CreateValidRequest(addressLine1: "First Address");
        var firstResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", firstRequest);
        AssertApiSuccess(firstResponse);
        var firstAddressId = firstResponse!.Data.Id;

        var secondRequest = AddressTestDataV1.Creation.CreateValidRequest(addressLine1: "Second Address");
        var secondResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", secondRequest);
        AssertApiSuccess(secondResponse);
        var secondAddressId = secondResponse!.Data.Id;

        // Delete the first address
        var deleteResponse = await DeleteApiResponseAsync($"v1/addresses/{firstAddressId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Get user's addresses
        var getUserAddressesResponse = await Client.GetAsync("v1/addresses");

        // Assert
        getUserAddressesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await getUserAddressesResponse.Content.ReadAsStringAsync();
        content.Should().NotContain(firstAddressId.ToString());
        content.Should().Contain(secondAddressId.ToString());
    }

    #endregion

    #region Response Validation Tests

    [Fact]
    public async Task DeleteAddress_SuccessResponse_ShouldHaveCorrectFormat()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);

        var createRequest = AddressTestDataV1.Creation.CreateValidRequest();
        var createResponse = await PostApiResponseAsync<object, CreateAddressResponseV1>("v1/addresses", createRequest);
        AssertApiSuccess(createResponse);
        var addressId = createResponse!.Data.Id;

        // Act
        var response = await DeleteApiResponseAsync($"v1/addresses/{addressId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<string>>(content, JsonOptions);
        AssertApiSuccess(apiResponse);
        apiResponse.Should().NotBeNull();
        apiResponse!.Succeeded.Should().BeTrue();
        apiResponse.Data.Should().NotBeNullOrEmpty();
        apiResponse.Data.Should().Be("Address deleted successfully");
        apiResponse.Message.Should().NotBeNullOrEmpty();
        apiResponse.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task DeleteAddress_ErrorResponse_ShouldHaveCorrectFormat()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await DeleteAsync($"v1/addresses/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        // The response should be in ApiResponse format
        var apiResponse = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<string>>(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        apiResponse.Should().NotBeNull();
        apiResponse!.Succeeded.Should().BeFalse();
        apiResponse.Message.Should().NotBeNullOrEmpty();
        apiResponse.StatusCode.Should().Be(404);
    }

    #endregion
}
