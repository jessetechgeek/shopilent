using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Application.Features.Payments.Commands.ProcessWebhook.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Sales.Enums;

namespace Shopilent.Application.UnitTests.Features.Payments.Commands.V1;

public class ProcessWebhookCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public ProcessWebhookCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockOrderWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentService.Object);
        services.AddTransient(sp => Fixture.GetLogger<ProcessWebhookCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ProcessWebhookCommandV1>();
        });

        // Register validator
        services.AddTransient<IValidator<ProcessWebhookCommandV1>, ProcessWebhookCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ProcessWebhook_WithValidPaymentSucceededEvent_ReturnsSuccessfulResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var transactionId = "pi_test_transaction_123";

        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"payment_intent.succeeded\"}",
            Signature = "stripe_signature_123",
            Headers = new Dictionary<string, string> { { "stripe-signature", "stripe_signature_123" } }
        };

        var webhookResult = new WebhookResult
        {
            EventId = "evt_test_123",
            EventType = "payment_intent.succeeded",
            TransactionId = transactionId,
            IsProcessed = true,
            EventData = new Dictionary<string, object> { { "id", transactionId }, { "status", "succeeded" } }
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Processing)
            .WithPricing(90.00m, 5.00m, 5.00m, "USD") // subtotal, tax, shipping = $100 total
            .Build();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            Money.Create(100.00m, "USD").Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe,
            transactionId);

        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken))
            .ReturnsAsync(Result.Success(webhookResult));

        // Mock repository calls
        Fixture.MockPaymentWriteRepository
            .Setup(repo => repo.GetByExternalReferenceAsync(transactionId, CancellationToken))
            .ReturnsAsync(payment);

        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventId.Should().Be("evt_test_123");
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.TransactionId.Should().Be(transactionId);
        result.Value.IsProcessed.Should().BeTrue();

        // Verify payment service was called
        Fixture.MockPaymentService.Verify(
            service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken),
            Times.Once);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_WithInvalidProvider_ReturnsInvalidProviderError()
    {
        // Arrange
        var command = new ProcessWebhookCommandV1
        {
            Provider = "InvalidProvider",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"payment_intent.succeeded\"}",
            Signature = "signature_123",
            Headers = new Dictionary<string, string>()
        };

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentErrors.InvalidProvider.Code);

        // Verify payment service was not called
        Fixture.MockPaymentService.Verify(
            service => service.ProcessWebhookAsync(
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithPaymentServiceFailure_ReturnsFailureResult()
    {
        // Arrange
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"payment_intent.succeeded\"}",
            Signature = "invalid_signature",
            Headers = new Dictionary<string, string> { { "stripe-signature", "invalid_signature" } }
        };

        var webhookError = PaymentErrors.ProcessingFailed("Invalid signature");

        // Mock payment service failure
        Fixture.MockPaymentService
            .Setup(service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken))
            .ReturnsAsync(Result.Failure<WebhookResult>(webhookError));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(webhookError.Code);

        // Verify payment service was called
        Fixture.MockPaymentService.Verify(
            service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_WithPaymentFailedEvent_UpdatesPaymentAndOrderStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var transactionId = "pi_test_transaction_failed";

        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"payment_intent.payment_failed\"}",
            Signature = "stripe_signature_123",
            Headers = new Dictionary<string, string> { { "stripe-signature", "stripe_signature_123" } }
        };

        var webhookResult = new WebhookResult
        {
            EventId = "evt_test_123",
            EventType = "payment_intent.payment_failed",
            TransactionId = transactionId,
            IsProcessed = true,
            EventData = new Dictionary<string, object>
            {
                { "id", transactionId }, { "status", "failed" }, { "failure_reason", "insufficient_funds" }
            }
        };

        var user = new UserBuilder().WithId(userId).Build();
        var order = new OrderBuilder()
            .WithId(orderId)
            .WithStatus(OrderStatus.Pending)
            .WithPaymentStatus(PaymentStatus.Processing)
            .WithPricing(90.00m, 5.00m, 5.00m, "USD") // subtotal, tax, shipping = $100 total
            .Build();

        var paymentResult = Payment.Create(
            order.Id,
            user.Id,
            Money.Create(100.00m, "USD").Value,
            PaymentMethodType.CreditCard,
            PaymentProvider.Stripe,
            transactionId);

        paymentResult.IsSuccess.Should().BeTrue();
        var payment = paymentResult.Value;

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken))
            .ReturnsAsync(Result.Success(webhookResult));

        // Mock repository calls
        Fixture.MockPaymentWriteRepository
            .Setup(repo => repo.GetByExternalReferenceAsync(transactionId, CancellationToken))
            .ReturnsAsync(payment);

        Fixture.MockOrderWriteRepository
            .Setup(repo => repo.GetByIdAsync(orderId, CancellationToken))
            .ReturnsAsync(order);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().Be("payment_intent.payment_failed");
        result.Value.TransactionId.Should().Be(transactionId);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_WithUnprocessedEvent_SkipsOrderUpdates()
    {
        // Arrange
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"customer.created\"}",
            Signature = "stripe_signature_123",
            Headers = new Dictionary<string, string> { { "stripe-signature", "stripe_signature_123" } }
        };

        var webhookResult = new WebhookResult
        {
            EventId = "evt_test_123",
            EventType = "customer.created",
            TransactionId = null,
            IsProcessed = false, // Not processed
            EventData = new Dictionary<string, object> { { "id", "cus_test_customer" } }
        };

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken))
            .ReturnsAsync(Result.Success(webhookResult));

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().Be("customer.created");
        result.Value.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhook_WithPaymentNotFound_ContinuesSuccessfully()
    {
        // Arrange
        var transactionId = "pi_test_not_found";

        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = "{\"id\":\"evt_test_123\",\"type\":\"payment_intent.succeeded\"}",
            Signature = "stripe_signature_123",
            Headers = new Dictionary<string, string> { { "stripe-signature", "stripe_signature_123" } }
        };

        var webhookResult = new WebhookResult
        {
            EventId = "evt_test_123",
            EventType = "payment_intent.succeeded",
            TransactionId = transactionId,
            IsProcessed = true,
            EventData = new Dictionary<string, object> { { "id", transactionId }, { "status", "succeeded" } }
        };

        // Mock payment service
        Fixture.MockPaymentService
            .Setup(service => service.ProcessWebhookAsync(
                PaymentProvider.Stripe,
                command.WebhookPayload,
                command.Signature,
                command.Headers,
                CancellationToken))
            .ReturnsAsync(Result.Success(webhookResult));

        // Mock payment not found
        Fixture.MockPaymentWriteRepository
            .Setup(repo => repo.GetByExternalReferenceAsync(transactionId, CancellationToken))
            .ReturnsAsync((Payment)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.TransactionId.Should().Be(transactionId);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
