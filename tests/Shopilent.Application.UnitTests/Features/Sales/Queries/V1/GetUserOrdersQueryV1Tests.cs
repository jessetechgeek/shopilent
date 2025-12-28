using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Queries.GetUserOrders.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.UnitTests.Features.Sales.Queries.V1;

public class GetUserOrdersQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetUserOrdersQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUserReadRepository.Object);
        services.AddTransient(sp => Fixture.MockOrderReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetUserOrdersQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetUserOrdersQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidUserWithOrders_ReturnsUserOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        var user = new UserDto { Id = userId, FirstName = "John", LastName = "Doe", Email = "john.doe@example.com" };

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Subtotal = 100.00m,
                Tax = 10.00m,
                ShippingCost = 5.00m,
                Total = 115.00m,
                Currency = "USD",
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                ShippingMethod = "Standard",
                TrackingNumber = "TRACK-001",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Subtotal = 75.00m,
                Tax = 7.50m,
                ShippingCost = 3.50m,
                Total = 86.00m,
                Currency = "USD",
                Status = OrderStatus.Shipped,
                PaymentStatus = PaymentStatus.Succeeded,
                ShippingMethod = "Express",
                TrackingNumber = "TRACK-002",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Subtotal = 50.00m,
                Tax = 5.00m,
                ShippingCost = 2.50m,
                Total = 57.50m,
                Currency = "USD",
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                ShippingMethod = "Standard",
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                UpdatedAt = DateTime.UtcNow.AddHours(-6)
            }
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        // Verify all orders belong to the user
        result.Value.Should().AllSatisfy(order => order.UserId.Should().Be(userId));

        // Verify order data
        var firstOrder = result.Value.First(o => o.Total == 115.00m);
        firstOrder.Total.Should().Be(115.00m);
        firstOrder.Status.Should().Be(OrderStatus.Delivered);
        firstOrder.TrackingNumber.Should().Be("TRACK-001");

        // Verify repository was called correctly
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidUserWithNoOrders_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        var user = new UserDto { Id = userId, FirstName = "Jane", LastName = "Doe", Email = "jane.doe@example.com" };

        var emptyOrders = new List<OrderDto>();

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(emptyOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();

        // Verify repository was called correctly
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        // Mock repository calls - user not found
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((UserDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify user repository was called, but order repository was not
        Fixture.MockUserReadRepository.Verify(
            repo => repo.GetByIdAsync(userId, CancellationToken),
            Times.Once);

        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserWithOrdersInDifferentStatuses_ReturnsAllOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        var user = new UserDto { Id = userId, FirstName = "Test", LastName = "User", Email = "test@example.com" };

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                Total = 100.00m,
                CreatedAt = DateTime.UtcNow
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                Total = 150.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Canceled,
                Total = 75.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Refunded,
                Total = 200.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);

        // Verify all different statuses are included
        result.Value.Should().Contain(o => o.Status == OrderStatus.Pending);
        result.Value.Should().Contain(o => o.Status == OrderStatus.Delivered);
        result.Value.Should().Contain(o => o.Status == OrderStatus.Cancelled);
        result.Value.Should().Contain(o => o.PaymentStatus == PaymentStatus.Refunded);

        // Verify all orders belong to the user
        result.Value.Should().AllSatisfy(order => order.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task Handle_OrdersWithDifferentPaymentStatuses_ReturnsAllOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        var user = new UserDto { Id = userId, FirstName = "Payment", LastName = "User", Email = "payment@example.com" };

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                Total = 100.00m
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                Total = 150.00m
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Failed,
                Total = 75.00m
            }
        };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        // Verify different payment statuses are included
        result.Value.Should().Contain(o => o.PaymentStatus == PaymentStatus.Pending);
        result.Value.Should().Contain(o => o.PaymentStatus == PaymentStatus.Succeeded);
        result.Value.Should().Contain(o => o.PaymentStatus == PaymentStatus.Failed);
    }

    [Fact]
    public async Task Handle_DatabaseExceptionOnUserLookup_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        // Mock repository calls to throw exception
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("User database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Orders.GetUserOrdersFailed");
        result.Error.Message.Should().Contain("User database connection failed");
    }

    [Fact]
    public async Task Handle_DatabaseExceptionOnOrderLookup_ReturnsFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var query = new GetUserOrdersQueryV1 { UserId = userId };

        var user = new UserDto { Id = userId, FirstName = "Test", LastName = "User", Email = "test@example.com" };

        // Mock repository calls
        Fixture.MockUserReadRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Order database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Orders.GetUserOrdersFailed");
        result.Error.Message.Should().Contain("Order database connection failed");
    }
}
