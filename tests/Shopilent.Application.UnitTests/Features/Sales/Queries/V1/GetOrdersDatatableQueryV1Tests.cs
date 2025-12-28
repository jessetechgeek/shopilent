using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Queries.GetOrdersDatatable.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.UnitTests.Features.Sales.Queries.V1;

public class GetOrdersDatatableQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetOrdersDatatableQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetOrdersDatatableQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetOrdersDatatableQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsDataTableResult()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();

        var query = new GetOrdersDatatableQueryV1
        {
            Request = new DataTableRequest
            {
                Draw = 1,
                Start = 0,
                Length = 10,
                Search = new DataTableSearch { Value = "" },
                Order = new List<DataTableOrder>(),
                Columns = new List<DataTableColumn>()
            }
        };

        // Mock order data
        var mockOrders = new List<OrderDetailDto>
        {
            new OrderDetailDto
            {
                Id = order1Id,
                UserId = userId1,
                Subtotal = 100.00m,
                Tax = 10.00m,
                ShippingCost = 5.00m,
                Total = 115.00m,
                Currency = "USD",
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                ShippingMethod = "Standard",
                TrackingNumber = "TRACK-001",
                RefundedAmount = 0,
                RefundedAt = null,
                RefundReason = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<OrderItemDto>()
            },
            new OrderDetailDto
            {
                Id = order2Id,
                UserId = userId2,
                Subtotal = 75.00m,
                Tax = 7.50m,
                ShippingCost = 3.50m,
                Total = 86.00m,
                Currency = "USD",
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                ShippingMethod = "Express",
                TrackingNumber = null,
                RefundedAmount = 0,
                RefundedAt = null,
                RefundReason = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<OrderItemDto>()
            }
        };

        var dataTableResult = new DataTableResult<OrderDetailDto>(1, 2, 2, mockOrders);

        // Mock user data
        var user1 = new UserDto { Id = userId1, FirstName = "John", LastName = "Doe", Email = "john.doe@example.com" };

        var user2 = new UserDto
        {
            Id = userId2, FirstName = "Jane", LastName = "Smith", Email = "jane.smith@example.com"
        };

        // Mock order details
        var orderDetail1 = new OrderDetailDto
        {
            Id = order1Id,
            Items = new List<OrderItemDto>
            {
                new OrderItemDto { Id = Guid.NewGuid() }, new OrderItemDto { Id = Guid.NewGuid() }
            }
        };

        var orderDetail2 = new OrderDetailDto
        {
            Id = order2Id, Items = new List<OrderItemDto> { new OrderItemDto { Id = Guid.NewGuid() } }
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetOrderDetailDataTableAsync(It.IsAny<DataTableRequest>(), CancellationToken))
            .ReturnsAsync(dataTableResult);

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId1, CancellationToken))
            .ReturnsAsync(user1);

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId2, CancellationToken))
            .ReturnsAsync(user2);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(order1Id, CancellationToken))
            .ReturnsAsync(orderDetail1);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(order2Id, CancellationToken))
            .ReturnsAsync(orderDetail2);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(2);
        result.Value.RecordsFiltered.Should().Be(2);
        result.Value.Data.Should().HaveCount(2);

        // Verify first order data
        var firstOrder = result.Value.Data.First();
        firstOrder.Id.Should().Be(order1Id);
        firstOrder.UserEmail.Should().Be("john.doe@example.com");
        firstOrder.UserFullName.Should().Be("John Doe");
        firstOrder.Total.Should().Be(115.00m);
        firstOrder.Status.Should().Be(OrderStatus.Delivered);
        firstOrder.ItemsCount.Should().Be(2);

        // Verify second order data
        var secondOrder = result.Value.Data.Last();
        secondOrder.Id.Should().Be(order2Id);
        secondOrder.UserEmail.Should().Be("jane.smith@example.com");
        secondOrder.UserFullName.Should().Be("Jane Smith");
        secondOrder.Total.Should().Be(86.00m);
        secondOrder.Status.Should().Be(OrderStatus.Pending);
        secondOrder.ItemsCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptyResults_ReturnsEmptyDataTableResult()
    {
        // Arrange
        var query = new GetOrdersDatatableQueryV1
        {
            Request = new DataTableRequest
            {
                Draw = 1,
                Start = 0,
                Length = 10,
                Search = new DataTableSearch { Value = "" },
                Order = new List<DataTableOrder>(),
                Columns = new List<DataTableColumn>()
            }
        };

        var emptyResult = new DataTableResult<OrderDetailDto>(1, 0, 0, new List<OrderDetailDto>());

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetOrderDetailDataTableAsync(It.IsAny<DataTableRequest>(), CancellationToken))
            .ReturnsAsync(emptyResult);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(0);
        result.Value.RecordsFiltered.Should().Be(0);
        result.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrderWithoutUser_HandlesNullUserGracefully()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        var query = new GetOrdersDatatableQueryV1
        {
            Request = new DataTableRequest
            {
                Draw = 1,
                Start = 0,
                Length = 10,
                Search = new DataTableSearch { Value = "" },
                Order = new List<DataTableOrder>(),
                Columns = new List<DataTableColumn>()
            }
        };

        var mockOrders = new List<OrderDetailDto>
        {
            new OrderDetailDto
            {
                Id = orderId,
                UserId = null, // Order without user
                Subtotal = 50.00m,
                Tax = 5.00m,
                ShippingCost = 2.00m,
                Total = 57.00m,
                Currency = "USD",
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                ShippingMethod = "Standard",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<OrderItemDto>()
            }
        };

        var dataTableResult = new DataTableResult<OrderDetailDto>(1, 1, 1, mockOrders);

        var orderDetail = new OrderDetailDto
        {
            Id = orderId, Items = new List<OrderItemDto> { new OrderItemDto { Id = Guid.NewGuid() } }
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetOrderDetailDataTableAsync(It.IsAny<DataTableRequest>(), CancellationToken))
            .ReturnsAsync(dataTableResult);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetail);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Data.Should().HaveCount(1);

        var order = result.Value.Data.First();
        order.Id.Should().Be(orderId);
        order.UserId.Should().BeNull();
        order.UserEmail.Should().BeNull();
        order.UserFullName.Should().BeNull();
        order.ItemsCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OrderWithoutDetails_SetsItemsCountToZero()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetOrdersDatatableQueryV1
        {
            Request = new DataTableRequest
            {
                Draw = 1,
                Start = 0,
                Length = 10,
                Search = new DataTableSearch { Value = "" },
                Order = new List<DataTableOrder>(),
                Columns = new List<DataTableColumn>()
            }
        };

        var mockOrders = new List<OrderDetailDto>
        {
            new OrderDetailDto
            {
                Id = orderId,
                UserId = userId,
                Total = 100.00m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<OrderItemDto>()
            }
        };

        var dataTableResult = new DataTableResult<OrderDetailDto>(1, 1, 1, mockOrders);

        var user = new UserDto { Id = userId, FirstName = "Test", LastName = "User", Email = "test@example.com" };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetOrderDetailDataTableAsync(It.IsAny<DataTableRequest>(), CancellationToken))
            .ReturnsAsync(dataTableResult);

        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Order details not found
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync((OrderDetailDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var order = result.Value.Data.First();
        order.ItemsCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DatabaseException_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetOrdersDatatableQueryV1
        {
            Request = new DataTableRequest
            {
                Draw = 1,
                Start = 0,
                Length = 10,
                Search = new DataTableSearch { Value = "" },
                Order = new List<DataTableOrder>(),
                Columns = new List<DataTableColumn>()
            }
        };

        // Mock repository calls to throw exception
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetOrderDetailDataTableAsync(It.IsAny<DataTableRequest>(), CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Orders.GetDataTableFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }
}
