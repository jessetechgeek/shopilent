using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.ProcessOrderPartialRefund.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class ProcessOrderPartialRefundCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public ProcessOrderPartialRefundCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<ProcessOrderPartialRefundCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<ProcessOrderPartialRefundCommandV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidPartialRefund_ReturnsSuccess()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refundAmount = 50.00m;
        var refundReason = "Damaged item";

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = refundAmount,
            Currency = "USD",
            Reason = refundReason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Shipped)
            .WithPaymentStatus(PaymentStatus.Succeeded)
            .WithPricing(100.00m, 10.00m, 5.00m, "USD")
            .Build();

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
        result.Value.RefundAmount.Should().Be(refundAmount);
        result.Value.Currency.Should().Be("USD");
        result.Value.Reason.Should().Be(refundReason);
        result.Value.IsFullyRefunded.Should().BeFalse(); // Partial refund

        // Verify order was updated
        updatedOrder.Should().NotBeNull();
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Once);

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

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = 50.00m,
            Currency = "USD",
            Reason = "Damaged item"
        };

        // Mock repository calls - order not found
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
    public async Task Handle_InvalidAmount_ReturnsValidationError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = -10.00m, // Invalid negative amount
            Currency = "USD",
            Reason = "Test refund"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Shipped)
            .WithPaymentStatus(PaymentStatus.Succeeded)
            .WithPricing(100.00m, 10.00m, 5.00m, "USD")
            .Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // Should fail during Money.Create validation

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RefundAmountExceedsOrderTotal_ReturnsBusinessLogicError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refundAmount = 200.00m; // Exceeds order total of 115.00

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = refundAmount,
            Currency = "USD",
            Reason = "Refund exceeds total"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Shipped)
            .WithPaymentStatus(PaymentStatus.Succeeded)
            .WithPricing(100.00m, 10.00m, 5.00m, "USD") // Total: 115.00
            .Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The error should be from the domain logic that prevents refunding more than the total

        // Verify save was not called since refund failed
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderNotInRefundableState_ReturnsBusinessLogicError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = 50.00m,
            Currency = "USD",
            Reason = "Customer requested refund"
        };

        var user = new UserBuilder().WithId(userId).Build();
        // Create order in pending status - cannot be refunded
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Pending)
            .WithPricing(100.00m, 10.00m, 5.00m, "USD")
            .Build();

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // The error should be from the domain logic that prevents refunding pending orders

        // Verify save was not called since refund failed
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FullRefundThroughPartialRefund_MarksAsFullyRefunded()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refundAmount = 115.00m; // Full order total

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = refundAmount,
            Currency = "USD",
            Reason = "Full refund via partial"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Shipped)
            .WithPaymentStatus(PaymentStatus.Succeeded)
            .WithPricing(100.00m, 10.00m, 5.00m, "USD") // Total: 115.00
            .Build();

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
        result.Value.RefundAmount.Should().Be(refundAmount);
        result.Value.IsFullyRefunded.Should().BeTrue();
        result.Value.RemainingAmount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DatabaseException_ReturnsFailureResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPartialRefundCommandV1
        {
            OrderId = orderId,
            Amount = 50.00m,
            Currency = "USD",
            Reason = "Test refund"
        };

        // Mock repository calls to throw exception
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.PartialRefundFailed");
        result.Error.Message.Should().Contain("Database connection failed");
    }
}
