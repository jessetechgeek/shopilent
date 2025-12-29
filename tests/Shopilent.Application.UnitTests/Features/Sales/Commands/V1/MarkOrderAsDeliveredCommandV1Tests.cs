using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Sales.Commands.MarkOrderAsDelivered.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class MarkOrderAsDeliveredCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public MarkOrderAsDeliveredCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<MarkOrderAsDeliveredCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<MarkOrderAsDeliveredCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<MarkOrderAsDeliveredCommandV1>, MarkOrderAsDeliveredCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ValidShippedOrder_MarksAsDeliveredSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Shipped).Build();

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
        result.Value.Status.Should().Be(OrderStatus.Delivered);
        result.Value.Message.Should().Be("Order marked as delivered successfully");
        result.Value.UpdatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));

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

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
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
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderNotInShippedStatus_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
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
        // The actual error will depend on the domain logic implementation

        // Verify save was not called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderAlreadyDelivered_ReturnsSuccessWithoutChanges()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
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
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(OrderStatus.Delivered);

        // Verify save was called even though no changes were needed
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutAuthenticatedUser_MarksAsDeliveredSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder().WithId(orderId).WithUser(user).WithStatus(OrderStatus.Shipped).Build();

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
        result.Value.Status.Should().Be(OrderStatus.Delivered);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CancelledOrder_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsDeliveredCommandV1
        {
            OrderId = orderId
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
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }
}
