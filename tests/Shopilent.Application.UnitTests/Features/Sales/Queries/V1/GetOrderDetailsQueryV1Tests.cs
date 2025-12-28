using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Queries.GetOrderDetails.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Queries.V1;

public class GetOrderDetailsQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetOrderDetailsQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockOrderReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetOrderDetailsQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<GetOrderDetailsQueryV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<GetOrderDetailsQueryV1>, GetOrderDetailsQueryValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequestAsOwner_ReturnsOrderDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = userId,
            IsAdmin = false,
            IsManager = false
        };

        var orderDetails = new OrderDetailDto
        {
            Id = orderId,
            UserId = userId,
            Status = OrderStatus.Delivered,
            Total = 115.00m,
            Currency = "USD",
            Items = new List<OrderItemDto>
            {
                new OrderItemDto
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Quantity = 2,
                    UnitPrice = 50.00m,
                    TotalPrice = 100.00m
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetails);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Total.Should().Be(115.00m);
        result.Value.Status.Should().Be(OrderStatus.Delivered);

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(orderId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestAsAdmin_ReturnsOrderDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid(); // Different user

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = adminUserId,
            IsAdmin = true,
            IsManager = false
        };

        var orderDetails = new OrderDetailDto
        {
            Id = orderId,
            UserId = orderOwnerUserId, // Order belongs to different user
            Status = OrderStatus.Pending,
            Total = 75.00m,
            Currency = "USD",
            Items = new List<OrderItemDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetails);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.UserId.Should().Be(orderOwnerUserId);

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(orderId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestAsManager_ReturnsOrderDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid(); // Different user

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = managerUserId,
            IsAdmin = false,
            IsManager = true
        };

        var orderDetails = new OrderDetailDto
        {
            Id = orderId,
            UserId = orderOwnerUserId, // Order belongs to different user
            Status = OrderStatus.Shipped,
            Total = 200.00m,
            Currency = "USD",
            Items = new List<OrderItemDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetails);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.UserId.Should().Be(orderOwnerUserId);
    }

    [Fact]
    public async Task Handle_NonExistentOrder_ReturnsNotFoundError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = userId,
            IsAdmin = false,
            IsManager = false
        };

        // Mock repository calls - order not found
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync((OrderDetailDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.NotFound(orderId).Code);

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(orderId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthorizedUserAccessingOtherUserOrder_ReturnsForbiddenError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid(); // Different user

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = currentUserId,
            IsAdmin = false,
            IsManager = false
        };

        var orderDetails = new OrderDetailDto
        {
            Id = orderId,
            UserId = orderOwnerUserId, // Order belongs to different user
            Status = OrderStatus.Pending,
            Total = 50.00m,
            Currency = "USD",
            Items = new List<OrderItemDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetails);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.AccessDenied");
        result.Error.Message.Should().Be("You are not authorized to view this order");

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(orderId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsForbiddenError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid();

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = null, // Unauthenticated
            IsAdmin = false,
            IsManager = false
        };

        var orderDetails = new OrderDetailDto
        {
            Id = orderId,
            UserId = orderOwnerUserId,
            Status = OrderStatus.Pending,
            Total = 50.00m,
            Currency = "USD",
            Items = new List<OrderItemDto>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Mock repository calls
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(orderDetails);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.AccessDenied");

        // Verify repository was called correctly
        Fixture.MockOrderReadRepository.Verify(
            repo => repo.GetDetailByIdAsync(orderId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DatabaseException_ReturnsFailureResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetOrderDetailsQueryV1
        {
            OrderId = orderId,
            CurrentUserId = userId,
            IsAdmin = false,
            IsManager = false
        };

        // Mock repository calls to throw exception
        Fixture.MockOrderReadRepository
            .Setup(repo => repo.GetDetailByIdAsync(orderId, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.GetDetailsFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }
}
