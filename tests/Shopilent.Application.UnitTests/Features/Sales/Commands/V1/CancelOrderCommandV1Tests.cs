using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.CancelOrder.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class CancelOrderCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public CancelOrderCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<CancelOrderCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<CancelOrderCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<CancelOrderCommandV1>, CancelOrderCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidRequestByOrderOwner_CancelsOrderSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Customer requested cancellation";

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = reason,
            CurrentUserId = userId,
            IsAdmin = false,
            IsManager = false
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Capture order updates
        Order updatedOrder = null;
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken))
            .Callback<Order, CancellationToken>((o, _) => updatedOrder = o)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Reason.Should().Be(reason);
        result.Value.Status.Should().Be(OrderStatus.Cancelled);

        // Verify order was updated
        updatedOrder.Should().NotBeNull();
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestByAdmin_CancelsOrderSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reason = "Administrative cancellation";

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = reason,
            CurrentUserId = adminId,
            IsAdmin = true,
            IsManager = false
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Reason.Should().Be(reason);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentOrder_ReturnsNotFoundError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = "Test cancellation",
            CurrentUserId = userId,
            IsAdmin = false,
            IsManager = false
        };

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
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsForbiddenError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = "Unauthorized cancellation attempt",
            CurrentUserId = otherUserId, // Different user trying to cancel
            IsAdmin = false,
            IsManager = false
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.CancelDenied");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ReturnsForbiddenError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = "Test cancellation",
            CurrentUserId = null, // No user context
            IsAdmin = false,
            IsManager = false
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Pending).Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.CancelDenied");

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ManagerRole_CancelsOrderSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var reason = "Manager cancellation";

        var command = new CancelOrderCommandV1
        {
            OrderId = orderId,
            Reason = reason,
            CurrentUserId = managerId,
            IsAdmin = false,
            IsManager = true // Manager should be able to cancel
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Reason.Should().Be(reason);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
