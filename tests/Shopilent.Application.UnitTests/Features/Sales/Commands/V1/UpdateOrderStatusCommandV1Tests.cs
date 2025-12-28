using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.UpdateOrderStatus.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class UpdateOrderStatusCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public UpdateOrderStatusCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<UpdateOrderStatusCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<UpdateOrderStatusCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<UpdateOrderStatusCommandV1>, UpdateOrderStatusCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidStatusTransition_UpdatesStatusSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Order approved for processing";

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Processing,
            Reason = reason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Processing);
        result.Value.UpdatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentOrder_ReturnsNotFoundError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Processing
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Order not found
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync((Order)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.NotFound(orderId).Code);

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidStatusTransition_ReturnsValidationError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Delivered // Invalid: Cannot go from Pending to Delivered directly
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Cannot transition from Pending to Delivered");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameStatusUpdate_ReturnsValidationError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Pending // Same as current status
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Order is already Pending");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TransitionFromDeliveredStatus_ReturnsValidationError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Cancelled // Cannot cancel delivered order
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Delivered).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Message.Should().Contain("Cannot transition from Delivered to Cancelled");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCancellationFromProcessing_UpdatesStatusSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Customer requested cancellation";

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Cancelled,
            Reason = reason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Cancelled);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutAuthenticatedUser_UpdatesStatusSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = OrderStatus.Shipped
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).Build();

        // No authenticated user (system process)
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Shipped);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    public async Task Handle_ValidStatusTransitions_UpdatesStatusSuccessfully(OrderStatus fromStatus, OrderStatus toStatus)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateOrderStatusCommandV1
        {
            Id = orderId,
            Status = toStatus
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(fromStatus).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(orderId);
        result.Value.Status.Should().Be(toStatus);
    }
}
