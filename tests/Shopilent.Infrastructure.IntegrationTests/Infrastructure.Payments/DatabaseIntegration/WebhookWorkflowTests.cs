using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Application.Features.Payments.Commands.ProcessWebhook.V1;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Payments.DatabaseIntegration;

[Collection("IntegrationTests")]
public class WebhookWorkflowTests : IntegrationTestBase
{
    private IMediator _mediator = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IOrderWriteRepository _orderWriteRepository = null!;
    private IPaymentWriteRepository _paymentWriteRepository = null!;
    private IPaymentReadRepository _paymentReadRepository = null!;

    public WebhookWorkflowTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _mediator = GetService<IMediator>();
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _orderWriteRepository = GetService<IOrderWriteRepository>();
        _paymentWriteRepository = GetService<IPaymentWriteRepository>();
        _paymentReadRepository = GetService<IPaymentReadRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StripePaymentSuccessWebhook_ShouldUpdatePaymentStatusAndPersist()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m, "USD")
            .WithExternalReference("pi_test_webhook_success")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Simulate minimal valid Stripe webhook payload for payment success
        var webhookPayload = $$"""
        {
            "id": "evt_test_webhook",
            "object": "event",
            "api_version": "2025-06-30",
            "created": 1625097600,
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": null,
                "idempotency_key": null
            },
            "type": "payment_intent.succeeded",
            "data": {
                "object": {
                    "id": "{{payment.ExternalReference}}",
                    "object": "payment_intent",
                    "amount": 10000,
                    "currency": "usd",
                    "status": "succeeded",
                    "metadata": {
                        "orderId": "{{order.Id}}"
                    }
                }
            }
        }
        """;

        // Act - Process webhook through complete application flow
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null, // No signature validation in integration tests
            Headers = new Dictionary<string, string>()
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.TransactionId.Should().Be(payment.ExternalReference);
        result.Value.PaymentStatus.Should().Be(PaymentStatus.Succeeded);

        // Verify payment was updated in database
        var updatedPayment = await _paymentReadRepository.GetByExternalReferenceAsync(payment.ExternalReference);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task StripePaymentFailedWebhook_ShouldUpdatePaymentStatusWithError()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(75m, "EUR")
            .WithExternalReference("pi_test_webhook_failed")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Simulate minimal Stripe webhook payload for payment failure
        var webhookPayload = $$"""
        {
            "id": "evt_test_webhook_failed",
            "object": "event",
            "api_version": "2025-06-30",
            "created": 1625097600,
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": null,
                "idempotency_key": null
            },
            "type": "payment_intent.payment_failed",
            "data": {
                "object": {
                    "id": "{{payment.ExternalReference}}",
                    "object": "payment_intent",
                    "amount": 7500,
                    "currency": "eur",
                    "status": "requires_payment_method",
                    "last_payment_error": {
                        "code": "card_declined",
                        "message": "Your card was declined.",
                        "decline_code": "generic_decline"
                    },
                    "metadata": {
                        "orderId": "{{order.Id}}"
                    }
                }
            }
        }
        """;

        // Act - Process webhook through complete application flow
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null, // No signature validation in integration tests
            Headers = new Dictionary<string, string>()
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("payment_intent.payment_failed");
        result.Value.TransactionId.Should().Be(payment.ExternalReference);
        result.Value.PaymentStatus.Should().Be(PaymentStatus.Failed);
        result.Value.ProcessingMessage.Should().Be("Payment failed: Your card was declined.");

        // Verify payment was updated in database
        var updatedPayment = await _paymentReadRepository.GetByExternalReferenceAsync(payment.ExternalReference);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Failed);
        updatedPayment.ErrorMessage.Should().Be("Payment failed");
    }

    [Fact(Skip = "TODO: charge.refunded event not supported by StripeWebhookHandlerFactory")]
    public async Task StripeRefundWebhook_ShouldUpdatePaymentToRefundedStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(200m, "USD")
            .WithExternalReference("pi_test_webhook_refund")
            .WithStripeCard()
            .Build();

        // Mark payment as succeeded first
        payment.MarkAsSucceeded("pi_test_webhook_refund");

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Simulate Stripe webhook payload for refund
        var webhookPayload = $$"""
        {
            "id": "evt_test_webhook_refund",
            "object": "event",
            "type": "charge.refunded",
            "data": {
                "object": {
                    "id": "ch_test_charge",
                    "object": "charge",
                    "amount": 20000,
                    "currency": "usd",
                    "refunded": true,
                    "amount_refunded": 20000,
                    "payment_intent": "{{payment.ExternalReference}}",
                    "refunds": {
                        "data": [{
                            "id": "re_test_refund",
                            "amount": 20000,
                            "currency": "usd",
                            "reason": "requested_by_customer"
                        }]
                    }
                }
            }
        }
        """;

        // Act - Process webhook through complete application flow
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null, // No signature validation in integration tests
            Headers = new Dictionary<string, string>()
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("charge.refunded");
        result.Value.PaymentStatus.Should().Be(PaymentStatus.Refunded);

        // Verify payment was updated in database
        var updatedPayment = await _paymentReadRepository.GetByExternalReferenceAsync(payment.ExternalReference);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task WebhookForUnknownPayment_ShouldReturnSuccessButNotUpdateDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Simulate webhook for payment that doesn't exist in our system
        var webhookPayload = """
        {
            "id": "evt_test_unknown",
            "object": "event",
            "api_version": "2025-06-30",
            "created": 1625097600,
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": null,
                "idempotency_key": null
            },
            "type": "payment_intent.succeeded",
            "data": {
                "object": {
                    "id": "pi_unknown_payment_12345",
                    "object": "payment_intent",
                    "amount": 5000,
                    "currency": "usd",
                    "status": "succeeded"
                }
            }
        }
        """;

        // Act - Process webhook through complete application flow
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null, // No signature validation in integration tests
            Headers = new Dictionary<string, string>()
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.TransactionId.Should().Be("pi_unknown_payment_12345");

        // Verify no payment exists with this reference
        var payment = await _paymentReadRepository.GetByExternalReferenceAsync("pi_unknown_payment_12345");
        payment.Should().BeNull();
    }

    [Fact]
    public async Task InvalidWebhookSignature_ShouldReturnFailure()
    {
        // Arrange
        await ResetDatabaseAsync();

        var webhookPayload = """
        {
            "id": "evt_test_invalid",
            "object": "event",
            "type": "payment_intent.succeeded",
            "data": {
                "object": {
                    "id": "pi_test_invalid",
                    "status": "succeeded"
                }
            }
        }
        """;

        var headers = new Dictionary<string, string>
        {
            ["stripe-signature"] = "t=1625097600,v1=invalid_signature"
        };

        // Act - Test with invalid signature to verify signature validation
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = "invalid_signature",
            Headers = headers
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task MalformedWebhookPayload_ShouldReturnFailure()
    {
        // Arrange
        await ResetDatabaseAsync();

        var malformedPayload = """
        {
            "invalid": "json",
            "missing": "required_fields"
        """; // Missing closing brace intentionally

        var headers = new Dictionary<string, string>
        {
            ["stripe-signature"] = "t=1625097600,v1=valid_signature_here"
        };

        // Act
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = malformedPayload,
            Signature = "valid_signature_here",
            Headers = headers
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
    }

    [Fact]
    public async Task UnsupportedWebhookEventType_ShouldReturnSuccessWithoutProcessing()
    {
        // Arrange
        await ResetDatabaseAsync();

        var webhookPayload = """
        {
            "id": "evt_test_unsupported",
            "object": "event",
            "api_version": "2025-06-30",
            "created": 1625097600,
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": null,
                "idempotency_key": null
            },
            "type": "charge.refunded",
            "data": {
                "object": {
                    "id": "ch_test_charge",
                    "object": "charge",
                    "refunded": true
                }
            }
        }
        """;

        // Act - Process webhook through complete application flow
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null, // No signature validation in integration tests
            Headers = new Dictionary<string, string>()
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("charge.refunded");
        result.Value.IsProcessed.Should().BeTrue(); // Unsupported events are marked as processed to avoid retries
    }

    [Fact]
    public async Task ConcurrentWebhookProcessing_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order1 = OrderBuilder.Random().WithUser(user).Build();
        var order2 = OrderBuilder.Random().WithUser(user).Build();

        var payment1 = PaymentBuilder.Random()
            .WithOrder(order1)
            .WithUser(user)
            .WithAmount(100m, "USD")
            .WithExternalReference("pi_concurrent_1")
            .WithStripeCard()
            .Build();

        var payment2 = PaymentBuilder.Random()
            .WithOrder(order2)
            .WithUser(user)
            .WithAmount(150m, "USD")
            .WithExternalReference("pi_concurrent_2")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order1);
        await _orderWriteRepository.AddAsync(order2);
        await _paymentWriteRepository.AddAsync(payment1);
        await _paymentWriteRepository.AddAsync(payment2);
        await _unitOfWork.CommitAsync();

        // Act - Process webhooks concurrently
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                using var scope = ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var payload = $$"""
                {
                    "id": "evt_concurrent_1",
                    "object": "event",
                    "api_version": "2025-06-30",
                    "created": 1625097600,
                    "livemode": false,
                    "pending_webhooks": 1,
                    "request": {
                        "id": null,
                        "idempotency_key": null
                    },
                    "type": "payment_intent.succeeded",
                    "data": {
                        "object": {
                            "id": "pi_concurrent_1",
                            "object": "payment_intent",
                            "status": "succeeded",
                            "amount": 10000,
                            "currency": "usd",
                            "customer": null,
                            "metadata": {
                                "orderId": "{{order1.Id}}"
                            }
                        }
                    }
                }
                """;

                var command = new ProcessWebhookCommandV1
                {
                    Provider = "Stripe",
                    WebhookPayload = payload,
                    Signature = null,
                    Headers = new Dictionary<string, string>()
                };

                return await mediator.Send(command);
            }),
            Task.Run(async () =>
            {
                using var scope = ServiceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var payload = $$"""
                {
                    "id": "evt_concurrent_2",
                    "object": "event",
                    "api_version": "2025-06-30",
                    "created": 1625097600,
                    "livemode": false,
                    "pending_webhooks": 1,
                    "request": {
                        "id": null,
                        "idempotency_key": null
                    },
                    "type": "payment_intent.payment_failed",
                    "data": {
                        "object": {
                            "id": "pi_concurrent_2",
                            "object": "payment_intent",
                            "status": "requires_payment_method",
                            "amount": 15000,
                            "currency": "usd",
                            "customer": null,
                            "last_payment_error": {
                                "message": "Concurrent test failure"
                            },
                            "metadata": {
                                "orderId": "{{order2.Id}}"
                            }
                        }
                    }
                }
                """;

                var command = new ProcessWebhookCommandV1
                {
                    Provider = "Stripe",
                    WebhookPayload = payload,
                    Signature = null,
                    Headers = new Dictionary<string, string>()
                };

                return await mediator.Send(command);
            })
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.IsSuccess);

        // Verify database updates
        var updatedPayment1 = await _paymentReadRepository.GetByExternalReferenceAsync("pi_concurrent_1");
        var updatedPayment2 = await _paymentReadRepository.GetByExternalReferenceAsync("pi_concurrent_2");

        updatedPayment1.Should().NotBeNull();
        updatedPayment1!.Status.Should().Be(PaymentStatus.Succeeded);

        updatedPayment2.Should().NotBeNull();
        updatedPayment2!.Status.Should().Be(PaymentStatus.Failed);
        updatedPayment2.ErrorMessage.Should().Be("Payment failed");
    }

    [Fact]
    public async Task WebhookRetryScenario_ShouldBeIdempotent()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(80m, "USD")
            .WithExternalReference("pi_test_idempotent")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        var webhookPayload = $$"""
        {
            "id": "evt_test_idempotent",
            "object": "event",
            "api_version": "2025-06-30",
            "created": 1625097600,
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": null,
                "idempotency_key": null
            },
            "type": "payment_intent.succeeded",
            "data": {
                "object": {
                    "id": "{{payment.ExternalReference}}",
                    "object": "payment_intent",
                    "amount": 8000,
                    "currency": "usd",
                    "status": "succeeded",
                    "metadata": {
                        "orderId": "{{order.Id}}"
                    }
                }
            }
        }
        """;

        // Act - Process the same webhook twice (retry scenario)
        var command = new ProcessWebhookCommandV1
        {
            Provider = "Stripe",
            WebhookPayload = webhookPayload,
            Signature = null,
            Headers = new Dictionary<string, string>()
        };

        var firstResult = await _mediator.Send(command);
        var secondResult = await _mediator.Send(command);

        // Assert
        firstResult.Should().NotBeNull();
        firstResult.IsSuccess.Should().BeTrue();

        secondResult.Should().NotBeNull();
        secondResult.IsSuccess.Should().BeTrue();

        // Verify payment is still in the correct state (idempotent)
        var finalPayment = await _paymentReadRepository.GetByExternalReferenceAsync(payment.ExternalReference);
        finalPayment.Should().NotBeNull();
        finalPayment!.Status.Should().Be(PaymentStatus.Succeeded);
    }
}
