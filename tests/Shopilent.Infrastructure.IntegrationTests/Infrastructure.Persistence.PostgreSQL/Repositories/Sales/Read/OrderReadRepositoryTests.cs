using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Read;

[Collection("IntegrationTests")]
public class OrderReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IAddressWriteRepository _addressWriteRepository = null!;

    public OrderReadRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _addressWriteRepository = GetService<IAddressWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingOrder_ShouldReturnOrder()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.UserId.Should().Be(user.Id);
        result.Status.Should().Be(order.Status);
        result.PaymentStatus.Should().Be(order.PaymentStatus);
        result.CreatedAt.Should().BeCloseTo(order.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.OrderReader.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailByIdAsync_ExistingOrder_ShouldReturnDetailedOrder()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .AsPaidOrder()
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.UserId.Should().Be(user.Id);
        result.Status.Should().Be(OrderStatus.Processing); // Paid order status
        result.PaymentStatus.Should().Be(order.PaymentStatus);
        result.Subtotal.Should().Be(order.Subtotal.Amount);
        result.Tax.Should().Be(order.Tax.Amount);
        result.ShippingCost.Should().Be(order.ShippingCost.Amount);
        result.Total.Should().Be(order.Total.Amount);
        result.CreatedAt.Should().BeCloseTo(order.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetDetailByIdAsync_NonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.OrderReader.GetDetailByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUserWithOrders_ShouldReturnUserOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var otherUser = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.UserWriter.AddAsync(otherUser);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var order2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).AsPaidOrder().Build();
        var otherUserOrder = new OrderBuilder().WithUser(otherUser).WithShippingAddress(shippingAddress).Build();

        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.OrderWriter.AddAsync(otherUserOrder);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(new[] { order1.Id, order2.Id });
        result.Should().OnlyContain(o => o.UserId == user.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.OrderReader.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByStatusAsync_ExistingOrdersWithStatus_ShouldReturnMatchingOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var pendingOrder1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var pendingOrder2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var shippedOrder = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).AsShippedOrder().Build();

        await _unitOfWork.OrderWriter.AddAsync(pendingOrder1);
        await _unitOfWork.OrderWriter.AddAsync(pendingOrder2);
        await _unitOfWork.OrderWriter.AddAsync(shippedOrder);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetByStatusAsync(OrderStatus.Pending);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(new[] { pendingOrder1.Id, pendingOrder2.Id });
        result.Should().OnlyContain(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task GetByStatusAsync_NoOrdersWithStatus_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var pendingOrder = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(pendingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetByStatusAsync(OrderStatus.Delivered);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentOrdersAsync_HasOrders_ShouldReturnMostRecentOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.SaveChangesAsync();

        // Add a small delay to ensure different timestamps
        await Task.Delay(50);

        var order2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.SaveChangesAsync();

        await Task.Delay(50);

        var order3 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(order3);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetRecentOrdersAsync(2);

        // Assert
        result.Should().HaveCount(2);
        // Orders should be returned in descending order by creation date (most recent first)
        result.First().CreatedAt.Should().BeOnOrAfter(result.Last().CreatedAt);
    }

    [Fact]
    public async Task GetRecentOrdersAsync_RequestMoreThanAvailable_ShouldReturnAllOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act - Request 10 orders when only 1 exists
        var result = await _unitOfWork.OrderReader.GetRecentOrdersAsync(10);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetRecentOrdersAsync_EmptyRepository_ShouldReturnEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _unitOfWork.OrderReader.GetRecentOrdersAsync(5);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_ExistingIds_ShouldReturnMatchingOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var order2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var order3 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();

        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.OrderWriter.AddAsync(order3);
        await _unitOfWork.SaveChangesAsync();

        var requestedIds = new[] { order1.Id, order3.Id };

        // Act
        var result = await _unitOfWork.OrderReader.GetByIdsAsync(requestedIds);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(requestedIds);
        result.Should().OnlyContain(o => requestedIds.Contains(o.Id));
    }

    [Fact]
    public async Task GetByIdsAsync_SomeNonExistentIds_ShouldReturnOnlyExistingOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        var requestedIds = new[] { order.Id, Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var result = await _unitOfWork.OrderReader.GetByIdsAsync(requestedIds);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetOrderDetailDataTableAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _addressWriteRepository.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        // Create multiple orders
        for (int i = 0; i < 5; i++)
        {
            var order = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
            await _unitOfWork.OrderWriter.AddAsync(order);
        }
        await _unitOfWork.SaveChangesAsync();

        var request = new DataTableRequest
        {
            Start = 0,
            Length = 3,
            Search = new DataTableSearch { Value = "" },
            Order = new List<DataTableOrder>
            {
                new() { Column = 0, Dir = "desc" }
            }
        };

        // Act
        var result = await _unitOfWork.OrderReader.GetOrderDetailDataTableAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().HaveCount(3);
        result.RecordsTotal.Should().Be(5);
        result.RecordsFiltered.Should().Be(5);
        result.Data.Should().OnlyContain(o => o.UserId == user.Id);
    }

    [Fact]
    public async Task GetOrderDetailDataTableAsync_EmptyRepository_ShouldReturnEmptyResult()
    {
        // Arrange
        await ResetDatabaseAsync();

        var request = new DataTableRequest
        {
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "" }
        };

        // Act
        var result = await _unitOfWork.OrderReader.GetOrderDetailDataTableAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().BeEmpty();
        result.RecordsTotal.Should().Be(0);
        result.RecordsFiltered.Should().Be(0);
    }

    [Fact]
    public async Task ListAllAsync_HasOrders_ShouldReturnAllOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = new UserBuilder().Build();
        var user2 = new UserBuilder().Build();
        var shippingAddress1 = new AddressBuilder().WithUser(user1).Build();
        var shippingAddress2 = new AddressBuilder().WithUser(user2).Build();

        await _unitOfWork.UserWriter.AddAsync(user1);
        await _unitOfWork.UserWriter.AddAsync(user2);
        await _addressWriteRepository.AddAsync(shippingAddress1);
        await _addressWriteRepository.AddAsync(shippingAddress2);
        await _unitOfWork.SaveChangesAsync();

        var order1 = new OrderBuilder().WithUser(user1).WithShippingAddress(shippingAddress1).Build();
        var order2 = new OrderBuilder().WithUser(user2).WithShippingAddress(shippingAddress2).AsShippedOrder().Build();

        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderReader.ListAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(new[] { order1.Id, order2.Id });
        result.Select(o => o.UserId).Should().Contain(new Guid?[] { user1.Id, user2.Id });
    }
}
