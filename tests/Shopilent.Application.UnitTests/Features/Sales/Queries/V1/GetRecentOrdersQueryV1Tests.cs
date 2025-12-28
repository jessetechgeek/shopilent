using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Queries.GetRecentOrders.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.UnitTests.Features.Sales.Queries.V1;

public class GetRecentOrdersQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetRecentOrdersQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockOrderReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetRecentOrdersQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<GetRecentOrdersQueryV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<GetRecentOrdersQueryV1>, GetRecentOrdersQueryValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsRecentOrders()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1
        {
            Count = 5
        };

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Subtotal = 100.00m,
                Tax = 10.00m,
                ShippingCost = 5.00m,
                Total = 115.00m,
                Currency = "USD",
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                ShippingMethod = "Standard",
                TrackingNumber = "TRACK-001",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
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
                UserId = Guid.NewGuid(),
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
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(5, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        // Verify order data
        var firstOrder = result.Value.First();
        firstOrder.Total.Should().Be(115.00m);
        firstOrder.Status.Should().Be(OrderStatus.Delivered);

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetRecentOrdersAsync(5, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DefaultCount_UsesDefaultValue()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1(); // Uses default count of 10

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Total = 100.00m,
                Status = OrderStatus.Delivered,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(10, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        // Verify repository was called with default count
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetRecentOrdersAsync(10, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoOrdersFound_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1
        {
            Count = 10
        };

        var emptyOrders = new List<OrderDto>();

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(10, CancellationToken))
            .ReturnsAsync(emptyOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetRecentOrdersAsync(10, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CustomCount_UsesSpecifiedCount()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1
        {
            Count = 20
        };

        var mockOrders = new List<OrderDto>();
        for (int i = 1; i <= 15; i++)
        {
            mockOrders.Add(new OrderDto
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Total = 50.00m + i,
                Status = OrderStatus.Delivered,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(20, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(15);

        // Verify repository was called with custom count
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetRecentOrdersAsync(20, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrdersInDifferentStatuses_ReturnsAllOrders()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1
        {
            Count = 3
        };

        var mockOrders = new List<OrderDto>
        {
            new OrderDto
            {
                Id = Guid.NewGuid(),
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                Total = 100.00m,
                CreatedAt = DateTime.UtcNow
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                Status = OrderStatus.Delivered,
                PaymentStatus = PaymentStatus.Succeeded,
                Total = 150.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new OrderDto
            {
                Id = Guid.NewGuid(),
                Status = OrderStatus.Cancelled,
                PaymentStatus = PaymentStatus.Refunded,
                Total = 75.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(3, CancellationToken))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        // Verify all different statuses are included
        result.Value.Should().Contain(o => o.Status == OrderStatus.Pending);
        result.Value.Should().Contain(o => o.Status == OrderStatus.Delivered);
        result.Value.Should().Contain(o => o.Status == OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_DatabaseException_ReturnsFailureResult()
    {
        // Arrange
        var query = new GetRecentOrdersQueryV1
        {
            Count = 10
        };

        // Mock repository calls to throw exception
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetRecentOrdersAsync(10, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Orders.GetRecentFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }
}
