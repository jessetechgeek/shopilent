using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;

namespace Shopilent.Application.UnitTests.Features.Sales.Commands.V1;

public class MarkOrderAsReturnedCommandV1Tests : TestBase
{
    private readonly MarkOrderAsReturnedCommandHandlerV1 _handler;

    public MarkOrderAsReturnedCommandV1Tests()
    {
        _handler = new MarkOrderAsReturnedCommandHandlerV1(
            Fixture.MockOrderWriteRepository.Object,
            Fixture.MockCurrentUserContext.Object,
            Fixture.MockUnitOfWork.Object,
            Fixture.GetLogger<MarkOrderAsReturnedCommandHandlerV1>());
    }

    [Fact]
    public async Task Handle_ValidDeliveredOrder_MarksAsReturnedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnReason = "Product not as described";

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = returnReason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());
        result.Value.ReturnReason.Should().Be(returnReason);
        result.Value.ReturnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        order.Status.Should().Be(OrderStatus.Returned);
        order.Metadata.Should().ContainKey("returnReason");
        order.Metadata["returnReason"].Should().Be(returnReason);
        order.Metadata.Should().ContainKey("returnedAt");

        // Verify update and commit were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidDeliveredOrderWithoutReason_MarksAsReturnedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = null
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());
        result.Value.ReturnReason.Should().BeNull();

        order.Status.Should().Be(OrderStatus.Returned);
        order.Metadata.Should().ContainKey("returnedAt");

        // Verify update and commit were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

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

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Product defective"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Order not found
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync((Order)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.NotFound(orderId).Code);
        result.Error.Message.Should().Be(OrderErrors.NotFound(orderId).Message);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderNotInDeliveredStatus_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Changed my mind"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Pending)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.InvalidOrderStatus("mark as returned - only delivered orders can be returned").Code);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShippedOrder_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Want to return"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Shipped)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.InvalidOrderStatus("mark as returned - only delivered orders can be returned").Code);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrderAlreadyReturned_ReturnsSuccessIdempotent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnReason = "Duplicate return request";

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = returnReason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Returned)
            .WithMetadata("returnReason", "Original reason")
            .WithMetadata("returnedAt", DateTime.UtcNow.AddDays(-1))
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());

        order.Status.Should().Be(OrderStatus.Returned);

        // Verify update and commit were still called (idempotent operation still commits)
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

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

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Attempting to return cancelled order"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Cancelled)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.InvalidOrderStatus("mark as returned - only delivered orders can be returned").Code);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsAccessDeniedError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Product damaged"
        };

        var orderOwner = new UserBuilder().WithId(orderOwnerUserId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(orderOwner)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup different authenticated user (not the order owner)
        Fixture.SetAuthenticatedUser(differentUserId, "different@example.com", isAdmin: false);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.AccessDenied.Code);
        result.Error.Message.Should().Be(OrderErrors.AccessDenied.Message);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AdminUser_CanMarkAnyOrderAsReturned()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Admin processed return"
        };

        var orderOwner = new UserBuilder().WithId(orderOwnerUserId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(orderOwner)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup admin user
        Fixture.SetAuthenticatedUser(adminUserId, "admin@example.com", isAdmin: true);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());

        // Verify update and commit were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ManagerUser_CanMarkAnyOrderAsReturned()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderOwnerUserId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Manager processed return"
        };

        var orderOwner = new UserBuilder().WithId(orderOwnerUserId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(orderOwner)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup manager user
        Fixture.SetAuthenticatedUser(managerUserId, "manager@example.com", isAdmin: false);
        Fixture.MockCurrentUserContext
            .Setup(ctx => ctx.IsInRole("Manager"))
            .Returns(true);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());

        // Verify update and commit were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OwnerUser_CanMarkOwnOrderAsReturned()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Customer initiated return"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup owner user
        Fixture.SetAuthenticatedUser(userId, "user@example.com", isAdmin: false);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be(OrderStatus.Returned.ToString());

        // Verify update and commit were called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExtractsReturnedAtFromMetadata()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnReason = "Product damaged";

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = returnReason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReturnedAt.Should().NotBe(default);
        result.Value.ReturnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        order.Metadata.Should().ContainKey("returnedAt");
        var returnedAt = (DateTime)order.Metadata["returnedAt"];
        returnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_LongReturnReason_StoresSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var longReturnReason = new string('A', 500); // Maximum allowed by validator

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = longReturnReason
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Delivered)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReturnReason.Should().Be(longReturnReason);
        order.Metadata["returnReason"].Should().Be(longReturnReason);
    }

    [Fact]
    public async Task Handle_ProcessingOrder_ReturnsFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = "Attempting to return processing order"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUser(user)
            .WithStatus(OrderStatus.Processing)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.InvalidOrderStatus("mark as returned - only delivered orders can be returned").Code);

        // Verify update and commit were not called
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Order>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }
}
