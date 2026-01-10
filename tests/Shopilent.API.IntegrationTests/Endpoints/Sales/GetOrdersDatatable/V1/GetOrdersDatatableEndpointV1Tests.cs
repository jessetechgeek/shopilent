using System.Net;
using Microsoft.EntityFrameworkCore;
using Shopilent.API.IntegrationTests.Common;
using Shopilent.Application.Features.Sales.Queries.GetOrdersDatatable.V1;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;

namespace Shopilent.API.IntegrationTests.Endpoints.Sales.GetOrdersDatatable.V1;

public class GetOrdersDatatableEndpointV1Tests : ApiIntegrationTestBase
{
    public GetOrdersDatatableEndpointV1Tests(ApiIntegrationTestWebFactory factory) : base(factory)
    {
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetOrdersDatatable_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Draw.Should().Be(request.Draw);
        response.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(0);
        response.Data.RecordsFiltered.Should().BeGreaterThanOrEqualTo(0);
        response.Data.Data.Should().NotBeNull();
        response.Data.Error.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetOrdersDatatable_WithTestOrders_ShouldReturnCorrectData()
    {
        // Arrange
        await EnsureCustomerUserExistsAsync();

        // Create test orders
        await CreateMultipleTestOrdersAsync(5);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest(length: 10);

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Data.Should().HaveCountGreaterThanOrEqualTo(5);
        response.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(5);
        response.Data.RecordsFiltered.Should().BeGreaterThanOrEqualTo(5);

        // Verify data structure
        var firstOrder = response.Data.Data.First();
        firstOrder.Id.Should().NotBeEmpty();
        firstOrder.UserId.Should().NotBeNull();
        firstOrder.UserEmail.Should().NotBeNullOrEmpty();
        firstOrder.UserFullName.Should().NotBeNullOrEmpty();
        firstOrder.Total.Should().BeGreaterThan(0);
        firstOrder.Currency.Should().NotBeNullOrEmpty();
        firstOrder.ShippingMethod.Should().NotBeNullOrEmpty();
        firstOrder.CreatedAt.Should().NotBe(default);
        firstOrder.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(10);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        // First page
        var firstPageRequest = GetOrdersDatatableTestDataV1.Pagination.CreateFirstPageRequest(pageSize: 5);

        // Act
        var firstPageResponse =
            await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", firstPageRequest);

        // Assert
        AssertApiSuccess(firstPageResponse);
        firstPageResponse!.Data.Data.Should().HaveCount(5);
        firstPageResponse.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(10);

        // Second page
        var secondPageRequest = GetOrdersDatatableTestDataV1.Pagination.CreateSecondPageRequest(pageSize: 5);
        var secondPageResponse =
            await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", secondPageRequest);

        AssertApiSuccess(secondPageResponse);
        secondPageResponse!.Data.Data.Should().HaveCount(5);

        // Verify different orders on different pages
        var firstPageIds = firstPageResponse.Data.Data.Select(o => o.Id).ToList();
        var secondPageIds = secondPageResponse.Data.Data.Select(o => o.Id).ToList();
        firstPageIds.Should().NotIntersectWith(secondPageIds);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithSearch_ShouldReturnFilteredResults()
    {
        // Arrange
        await EnsureCustomerUserExistsAsync();

        // Create test orders
        await CreateMultipleTestOrdersAsync(3);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SearchScenarios.CreateUserEmailSearchRequest("customer");

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Data.Should().HaveCountGreaterThanOrEqualTo(1);
        response.Data.Data.Should().AllSatisfy(o => o.UserEmail.Should().Contain("customer"));
    }

    [Fact]
    public async Task GetOrdersDatatable_WithSortingByTotal_ShouldReturnSortedResults()
    {
        // Arrange
        await CreateMultipleOrdersWithDifferentTotalsAsync();

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SortingScenarios.CreateSortByTotalRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Data.Should().HaveCountGreaterThanOrEqualTo(3);

        var sortedTotals = response.Data.Data.Select(o => o.Total).ToList();
        sortedTotals.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetOrdersDatatable_WithDescendingSortByCreatedAt_ShouldReturnNewestFirst()
    {
        // Arrange
        await CreateMultipleTestOrdersWithDelaysAsync(5);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SortingScenarios.CreateSortByCreatedAtRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();

        var sortedDates = response.Data.Data.Select(o => o.CreatedAt).ToList();
        sortedDates.Should().BeInDescendingOrder();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetOrdersDatatable_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        ClearAuthenticationHeader();
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithCustomerRole_ShouldReturnForbidden()
    {
        // Arrange
        var accessToken = await AuthenticateAsCustomerAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithAdminRole_ShouldReturnSuccess()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrdersDatatable_WithManagerRole_ShouldReturnSuccess()
    {
        // Arrange
        await EnsureManagerUserExistsAsync();
        var accessToken = await AuthenticateAsync("manager@shopilent.com", "Manager123!");
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GetOrdersDatatable_WithZeroLength_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.Pagination.CreateZeroLengthRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetOrdersDatatable_WithNegativeValues_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.ValidationTests.CreateNegativeStartRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithExcessiveLength_ShouldReturnValidationError()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.ValidationTests.CreateExcessiveLengthRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Search Scenarios Tests

    [Fact]
    public async Task GetOrdersDatatable_WithNoResultsSearch_ShouldReturnEmptyData()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SearchScenarios.CreateNoResultsSearchRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Data.Should().BeEmpty();
        response.Data.RecordsFiltered.Should().Be(0);
        response.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithStatusSearch_ShouldFilterByStatus()
    {
        // Arrange
        await CreateOrdersWithDifferentStatusesAsync();

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SearchScenarios.CreateStatusSearchRequest("pending");

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        if (response.Data.Data.Any())
        {
            response.Data.Data.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Pending));
        }
    }

    [Fact]
    public async Task GetOrdersDatatable_WithShippingMethodSearch_ShouldFilterByShippingMethod()
    {
        // Arrange
        await CreateOrdersWithDifferentShippingMethodsAsync();

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SearchScenarios.CreateShippingMethodSearchRequest("express");

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        if (response.Data.Data.Any())
        {
            response.Data.Data.Should().AllSatisfy(o =>
                o.ShippingMethod.Should().Contain("Express", "ShippingMethod should contain Express"));
        }
    }

    [Fact]
    public async Task GetOrdersDatatable_WithEmptySearch_ShouldReturnAllOrders()
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(3);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SearchScenarios.CreateEmptySearchRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.RecordsFiltered.Should().Be(response.Data.RecordsTotal);
        response.Data.Data.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task GetOrdersDatatable_WithHighPageNumber_ShouldReturnEmptyOrLastPage()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.Pagination.CreateHighStartRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Data.Should().BeEmpty(); // No orders at such high page number
        response.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetOrdersDatatable_WithComplexRequest_ShouldHandleAllParameters()
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(10);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.EdgeCases.CreateComplexRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();
        response.Data.Draw.Should().Be(request.Draw);
        response.Data.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrdersDatatable_WithInvalidColumnSort_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.SortingScenarios.CreateInvalidColumnSortRequest();

        // Act
        var response = await PostAsync("v1/orders/datatable", request);

        // Assert - Should either return BadRequest or handle gracefully with default sort
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrdersDatatable_ConcurrentRequests_ShouldHandleGracefully()
    {
        // Arrange
        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);

        var requests = Enumerable.Range(0, 5)
            .Select(i => GetOrdersDatatableTestDataV1.CreateValidRequest(draw: i + 1))
            .ToList();

        // Act
        var tasks = requests.Select(request =>
            PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request)
        ).ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response => AssertApiSuccess(response));
        responses.Should().HaveCount(5);

        // Verify each response has correct draw number
        for (int i = 0; i < responses.Length; i++)
        {
            responses[i]!.Data.Draw.Should().Be(i + 1);
        }
    }

    [Fact]
    public async Task GetOrdersDatatable_DatabaseConsistency_ShouldMatchDatabaseCounts()
    {
        // Arrange
        var orderIds = await CreateMultipleTestOrdersAsync(5);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest(length: 100); // Get all orders

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);

        // Verify against database
        await ExecuteDbContextAsync(async context =>
        {
            var totalOrdersInDb = await context.Orders.CountAsync();
            response!.Data.RecordsTotal.Should().Be(totalOrdersInDb);
            response.Data.RecordsFiltered.Should().Be(totalOrdersInDb);

            // Verify specific test orders are included
            var responseIds = response.Data.Data.Select(o => o.Id).ToList();
            responseIds.Should().Contain(orderIds);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(25)]
    public async Task GetOrdersDatatable_WithDifferentPageSizes_ShouldReturnCorrectCount(int pageSize)
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(15);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest(length: pageSize);

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Should().NotBeNull();

        // Should return at most pageSize items
        response.Data.Data.Should().HaveCountLessOrEqualTo(pageSize);
        response.Data.RecordsTotal.Should().BeGreaterThanOrEqualTo(response.Data.Data.Count);
    }

    [Fact]
    public async Task GetOrdersDatatable_ResponseTime_ShouldBeReasonable()
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(50);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);
        stopwatch.Stop();

        // Assert
        AssertApiSuccess(response);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10)); // Should be fast enough for UI
    }

    #endregion

    #region Specific Order Data Tests

    [Fact]
    public async Task GetOrdersDatatable_ShouldIncludeAllRequiredFields()
    {
        // Arrange
        await CreateMultipleTestOrdersAsync(3);

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest();

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Data.Should().NotBeEmpty();

        // Verify each order has all required fields
        response.Data.Data.Should().AllSatisfy(order =>
        {
            order.Id.Should().NotBeEmpty();
            order.UserId.Should().NotBeNull();
            order.UserEmail.Should().NotBeNullOrEmpty();
            order.UserFullName.Should().NotBeNullOrEmpty();
            order.Subtotal.Should().BeGreaterThan(0);
            order.Tax.Should().BeGreaterThanOrEqualTo(0);
            order.ShippingCost.Should().BeGreaterThanOrEqualTo(0);
            order.Total.Should().BeGreaterThan(0);
            order.Currency.Should().NotBeNullOrEmpty();
            order.ShippingMethod.Should().NotBeNullOrEmpty();
            order.ItemsCount.Should().BeGreaterThan(0);
            order.CreatedAt.Should().NotBe(default);
            order.UpdatedAt.Should().NotBe(default);
        });
    }

    [Fact]
    public async Task GetOrdersDatatable_WithOrdersFromMultipleUsers_ShouldReturnAllOrders()
    {
        // Arrange
        await CreateOrdersForMultipleUsersAsync();

        var accessToken = await AuthenticateAsAdminAsync();
        SetAuthenticationHeader(accessToken);
        var request = GetOrdersDatatableTestDataV1.CreateValidRequest(length: 50);

        // Act
        var response = await PostDataTableResponseAsync<OrderDatatableDto>("v1/orders/datatable", request);

        // Assert
        AssertApiSuccess(response);
        response!.Data.Data.Should().NotBeEmpty();

        // Verify we have orders from multiple users
        var uniqueUserIds = response.Data.Data.Select(o => o.UserId).Distinct().Count();
        uniqueUserIds.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates multiple test orders for testing
    /// </summary>
    private async Task<List<Guid>> CreateMultipleTestOrdersAsync(int count)
    {
        var orderIds = new List<Guid>();
        for (int i = 0; i < count; i++)
        {
            var orderId = await CreateSingleTestOrderAsync();
            orderIds.Add(orderId);
        }
        return orderIds;
    }

    /// <summary>
    /// Creates multiple test orders with slight delays to ensure different timestamps
    /// </summary>
    private async Task CreateMultipleTestOrdersWithDelaysAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await CreateSingleTestOrderAsync();
            await Task.Delay(10); // Small delay to ensure different timestamps
        }
    }

    /// <summary>
    /// Creates a single test order
    /// </summary>
    private async Task<Guid> CreateSingleTestOrderAsync()
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                await EnsureCustomerUserExistsAsync();
                user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            }

            if (user == null)
                throw new InvalidOperationException("Could not create or find customer user.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 99.99m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            var productSnapshot = ProductSnapshot.Create(
                name: product.Name,
                sku: product.Sku,
                slug: product.Slug.Value
            ).Value;

            order.AddItem(product.Id, null, 1, Money.Create(99.99m, "USD").Value, productSnapshot);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates orders with different totals for sorting tests
    /// </summary>
    private async Task CreateMultipleOrdersWithDifferentTotalsAsync()
    {
        await CreateOrderWithSpecificTotalAsync(50.00m);
        await CreateOrderWithSpecificTotalAsync(150.00m);
        await CreateOrderWithSpecificTotalAsync(100.00m);
    }

    /// <summary>
    /// Creates an order with a specific total amount
    /// </summary>
    private async Task<Guid> CreateOrderWithSpecificTotalAsync(decimal totalAmount)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                await EnsureCustomerUserExistsAsync();
                user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            }

            if (user == null)
                throw new InvalidOperationException("Could not create or find customer user.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", totalAmount - 13.00m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(totalAmount - 13.00m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: "Standard"
            ).Value;

            var productSnapshot = ProductSnapshot.Create(
                name: product.Name,
                sku: product.Sku,
                slug: product.Slug.Value
            ).Value;

            order.AddItem(product.Id, null, 1, Money.Create(totalAmount - 13.00m, "USD").Value, productSnapshot);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates orders with different statuses
    /// </summary>
    private async Task CreateOrdersWithDifferentStatusesAsync()
    {
        await CreateOrderWithSpecificStatusAsync(OrderStatus.Pending);
        await CreateOrderWithSpecificStatusAsync(OrderStatus.Processing);
        await CreateOrderWithSpecificStatusAsync(OrderStatus.Shipped);
    }

    /// <summary>
    /// Creates an order with a specific status
    /// </summary>
    private async Task<Guid> CreateOrderWithSpecificStatusAsync(OrderStatus status)
    {
        var orderId = await CreateSingleTestOrderAsync();

        await ExecuteDbContextAsync(async context =>
        {
            var order = await context.Orders.FindAsync(orderId);
            if (order == null) return;

            switch (status)
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

        return orderId;
    }

    /// <summary>
    /// Creates orders with different shipping methods
    /// </summary>
    private async Task CreateOrdersWithDifferentShippingMethodsAsync()
    {
        await CreateOrderWithSpecificShippingMethodAsync("Standard");
        await CreateOrderWithSpecificShippingMethodAsync("Express");
        await CreateOrderWithSpecificShippingMethodAsync("Overnight");
    }

    /// <summary>
    /// Creates an order with a specific shipping method
    /// </summary>
    private async Task<Guid> CreateOrderWithSpecificShippingMethodAsync(string shippingMethod)
    {
        return await ExecuteDbContextAsync(async context =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                await EnsureCustomerUserExistsAsync();
                user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer@shopilent.com");
            }

            if (user == null)
                throw new InvalidOperationException("Could not create or find customer user.");

            var address = await CreateTestAddressForUserAsync(context, user);
            var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 99.99m);

            var order = Order.Create(
                userId: user.Id,
                shippingAddressId: address.Id,
                billingAddressId: address.Id,
                subtotal: Money.Create(99.99m, "USD").Value,
                tax: Money.Create(8.00m, "USD").Value,
                shippingCost: Money.Create(5.00m, "USD").Value,
                shippingMethod: shippingMethod
            ).Value;

            var productSnapshot = ProductSnapshot.Create(
                name: product.Name,
                sku: product.Sku,
                slug: product.Slug.Value
            ).Value;

            order.AddItem(product.Id, null, 1, Money.Create(99.99m, "USD").Value, productSnapshot);

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return order.Id;
        });
    }

    /// <summary>
    /// Creates orders for multiple users
    /// </summary>
    private async Task CreateOrdersForMultipleUsersAsync()
    {
        // Ensure first customer exists and create orders
        await EnsureCustomerUserExistsAsync();
        await CreateMultipleTestOrdersAsync(2);

        // Ensure second customer exists
        await EnsureSecondCustomerExistsAsync();

        // Create orders for the second customer
        await CreateOrdersForSecondCustomerAsync(2);
    }

    /// <summary>
    /// Ensures the second customer user exists
    /// </summary>
    private async Task EnsureSecondCustomerExistsAsync()
    {
        var userExists = await ExecuteDbContextAsync(async context =>
        {
            return await context.Users.AnyAsync(u => u.Email.Value == "customer2@shopilent.com");
        });

        if (!userExists)
        {
            await RegisterSecondCustomerAsync();
        }
    }

    /// <summary>
    /// Registers a second customer user for testing
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
    }

    /// <summary>
    /// Creates orders for the second customer
    /// </summary>
    private async Task CreateOrdersForSecondCustomerAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ExecuteDbContextAsync(async context =>
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == "customer2@shopilent.com");
                if (user == null)
                    throw new InvalidOperationException("Second customer user not found.");

                var address = await CreateTestAddressForUserAsync(context, user);
                var product = await CreateTestProductAsync(context, $"Product-{Guid.NewGuid():N}", 49.99m);

                var order = Order.Create(
                    userId: user.Id,
                    shippingAddressId: address.Id,
                    billingAddressId: address.Id,
                    subtotal: Money.Create(49.99m, "USD").Value,
                    tax: Money.Create(4.00m, "USD").Value,
                    shippingCost: Money.Create(5.00m, "USD").Value,
                    shippingMethod: "Express"
                ).Value;

                var productSnapshot = ProductSnapshot.Create(
                    name: product.Name,
                    sku: product.Sku,
                    slug: product.Slug.Value
                ).Value;

                order.AddItem(product.Id, null, 1, Money.Create(49.99m, "USD").Value, productSnapshot);

                context.Orders.Add(order);
                await context.SaveChangesAsync();
            });
        }
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
