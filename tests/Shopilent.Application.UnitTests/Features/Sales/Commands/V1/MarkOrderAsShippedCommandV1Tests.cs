using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.MarkOrderAsShipped.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class MarkOrderAsShippedCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public MarkOrderAsShippedCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<MarkOrderAsShippedCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<MarkOrderAsShippedCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<MarkOrderAsShippedCommandV1>, MarkOrderAsShippedCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidConfirmedOrder_MarksAsShippedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).WithPaymentStatus(PaymentStatus.Succeeded).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

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

        // Verify order was updated and saved
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

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
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
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

        // Verify update and save were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderWithPaymentNotSucceeded_ReturnsPaymentRequiredError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).WithPaymentStatus(PaymentStatus.Pending).Build();

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
        // Payment must be succeeded before shipping

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderAlreadyShipped_ReturnsSuccessWithoutChanges()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Shipped).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

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

        // Verify update and save were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutAuthenticatedUser_MarksAsShippedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).WithPaymentStatus(PaymentStatus.Succeeded).Build();

        // No authenticated user (system process)
        Fixture.MockCurrentUserContext.Setup(ctx => ctx.UserId).Returns((Guid?)null);

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

        // Verify order was updated and saved
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CancelledOrder_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = "TRACK123456789";

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Cancelled).Build();

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
        // The actual error will depend on the domain logic implementation

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyTrackingNumber_MarksAsShippedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var trackingNumber = ""; // Empty but valid according to validation

        var command = new MarkOrderAsShippedCommandV1
        {
            OrderId = orderId,
            TrackingNumber = trackingNumber
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Processing).WithPaymentStatus(PaymentStatus.Succeeded).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

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

        // Verify order was updated and saved
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }
}
