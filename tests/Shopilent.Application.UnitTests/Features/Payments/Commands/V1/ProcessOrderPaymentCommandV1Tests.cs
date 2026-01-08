using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Application.Features.Payments.Commands.ProcessOrderPayment.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Application.UnitTests.Features.Payments.Commands.V1;

public class ProcessOrderPaymentCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public ProcessOrderPaymentCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockPaymentService.Object);
        services.AddTransient(sp => Fixture.GetLogger<ProcessOrderPaymentCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<ProcessOrderPaymentCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<ProcessOrderPaymentCommandV1>, ProcessOrderPaymentCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ProcessOrderPayment_WithValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            PaymentMethodId = paymentMethodId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789",
            Metadata = new Dictionary<string, object>
            {
                { "customer_ip", "192.168.1.1" },
                { "user_agent", "Test Agent" }
            }
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .WithPricing(90.00m, 5.00m, 5.00m, "USD") // subtotal, tax, shipping = $100 total
            .Build();

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = userId,
            Type = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            Token = "pm_test_123456789",
            DisplayName = "Visa ending in 4242",
            IsDefault = true,
            IsActive = true,
            Metadata = new Dictionary<string, object>
            {
                { "stripe_customer_id", "cus_test_customer" }
            }
        };

        var paymentResult = new PaymentResult
        {
            TransactionId = "pi_test_transaction",
            Status = PaymentStatus.Succeeded,
            ClientSecret = "pi_test_client_secret",
            RequiresAction = false,
            Metadata = new Dictionary<string, object>
            {
                { "provider_fee", 3.50m }
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessPaymentAsync(
                It.IsAny<Money>(),
                It.IsAny<PaymentMethodType>(),
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(paymentResult));

        // Capture payment being added
        Payment addedPayment = null;
        Fixture.MockPaymentWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken))
            .Callback<Payment, CancellationToken>((payment, _) => addedPayment = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Amount.Should().Be(100.00m);
        result.Value.Currency.Should().Be("USD");
        result.Value.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.MethodType.Should().Be(PaymentMethodType.CreditCard);
        result.Value.Provider.Should().Be(PaymentProvider.Stripe);
        result.Value.TransactionId.Should().Be("pi_test_transaction");
        result.Value.Message.Should().Be("Payment processed successfully");
        result.Value.RequiresAction.Should().BeFalse();

        // Verify payment was created and added
        addedPayment.Should().NotBeNull();
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Once);

        // Verify order was updated
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(order, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithNonExistentOrder_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock order not found
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync((Domain.Sales.Order)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(OrderErrors.NotFound(orderId).Code);

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithInvalidOrderStatus_ReturnsInvalidStatusError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Shipped) // Invalid status for payment
            .Build();

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
        result.Error.Code.Should().Be(OrderErrors.InvalidOrderStatus("payment processing").Code);

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithAlreadyPaidOrder_ReturnsInvalidPaymentStatusError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Succeeded) // Already paid
            .Build();

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
        result.Error.Code.Should().Be(PaymentErrors.InvalidPaymentStatus("duplicate payment").Code);

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithUnauthorizedUser_ReturnsUnauthorizedError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(otherUserId) // Different user
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .Build();

        // Setup authenticated user (different from order user)
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.UnauthorizedAccess");

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithNonExistentPaymentMethod_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            PaymentMethodId = paymentMethodId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync((PaymentMethodDto)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentErrors.PaymentMethodNotFound(paymentMethodId).Code);

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithPaymentMethodBelongingToDifferentUser_ReturnsUnauthorizedError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            PaymentMethodId = paymentMethodId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .Build();

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = otherUserId, // Belongs to different user
            Type = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            Token = "pm_test_123456789"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.UnauthorizedAccess");

        // Verify no payment was created
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithPaymentServiceFailure_ReturnsFailureAndRecordsFailedPayment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .WithPricing(90.00m, 5.00m, 5.00m, "USD") // subtotal, tax, shipping = $100 total
            .Build();

        var paymentError = PaymentErrors.InsufficientFunds;

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock payment service failure
        Fixture.MockPaymentService
            .Setup(service => service.ProcessPaymentAsync(
                It.IsAny<Money>(),
                It.IsAny<PaymentMethodType>(),
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                CancellationToken))
            .ReturnsAsync(Result.Failure<PaymentResult>(paymentError));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(paymentError.Code);

        // Verify failed payment was recorded
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Once);

        // Verify save was called to record the failed payment
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderPayment_WithPaymentRequiringAction_ReturnsSuccessWithActionRequired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var command = new ProcessOrderPaymentCommandV1
        {
            OrderId = orderId,
            MethodType = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            PaymentMethodToken = "pm_test_123456789"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithUserId(userId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Pending)
            .WithPricing(90.00m, 5.00m, 5.00m, "USD") // subtotal, tax, shipping = $100 total
            .Build();

        var paymentResult = new PaymentResult
        {
            TransactionId = "pi_test_transaction",
            Status = PaymentStatus.RequiresAction,
            ClientSecret = "pi_test_client_secret",
            RequiresAction = true,
            NextActionType = "use_stripe_sdk"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessPaymentAsync(
                It.IsAny<Money>(),
                It.IsAny<PaymentMethodType>(),
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                CancellationToken))
            .ReturnsAsync(Result.Success(paymentResult));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(PaymentStatus.RequiresAction);
        result.Value.Message.Should().Be("Payment requires additional authentication");
        result.Value.RequiresAction.Should().BeTrue();
        result.Value.NextActionType.Should().Be("use_stripe_sdk");

        // Verify payment was created but order was not marked as paid
        Fixture.MockPaymentWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<Payment>(), CancellationToken),
            Times.Once);

        // Order should not be updated since payment requires action
        Fixture.MockOrderWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<Domain.Sales.Order>(), CancellationToken),
            Times.Never);
    }
}
