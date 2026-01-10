using Microsoft.Extensions.Logging;
using Moq;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.Payments.Abstractions;
using Shopilent.Infrastructure.Payments.Models;
using Shopilent.Infrastructure.Payments.Services;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Payments.UnitTests.Services;

/// <summary>
/// Unit tests for PaymentService with mocked payment providers.
/// These tests verify the service orchestration logic without external dependencies.
/// </summary>
public class PaymentServiceUnitTests
{
    private readonly Mock<IPaymentProvider> _mockStripeProvider;
    private readonly Mock<IPaymentProvider> _mockPayPalProvider;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;

    public PaymentServiceUnitTests()
    {
        _mockStripeProvider = new Mock<IPaymentProvider>();
        _mockPayPalProvider = new Mock<IPaymentProvider>();
        _mockLogger = new Mock<ILogger<PaymentService>>();

        _mockStripeProvider.Setup(p => p.Provider).Returns(PaymentProvider.Stripe);
        _mockPayPalProvider.Setup(p => p.Provider).Returns(PaymentProvider.PayPal);

        var providers = new List<IPaymentProvider> { _mockStripeProvider.Object, _mockPayPalProvider.Object };
        _paymentService = new PaymentService(providers, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithValidStripeProvider_ShouldReturnSuccess()
    {
        // Arrange
        var amount = Money.Create(100m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_test_123";
        const string customerId = "cus_test_123";
        var metadata = new Dictionary<string, object> { ["order_id"] = "order_123" };

        var expectedResult = new PaymentResult
        {
            TransactionId = "pi_test_123",
            Status = PaymentStatus.Succeeded,
            ClientSecret = "pi_test_123_secret",
            RequiresAction = false
        };

        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount,
            methodType,
            provider,
            paymentMethodToken,
            customerId,
            metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TransactionId.Should().Be("pi_test_123");
        result.Value.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.RequiresAction.Should().BeFalse();

        _mockStripeProvider.Verify(p => p.ProcessPaymentAsync(
            It.Is<PaymentRequest>(r =>
                r.Amount == amount &&
                r.MethodType == methodType &&
                r.PaymentMethodToken == paymentMethodToken &&
                r.CustomerId == customerId &&
                r.Metadata.ContainsKey("order_id") &&
                r.Metadata["order_id"].ToString() == "order_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithInvalidProvider_ShouldReturnFailure()
    {
        // Arrange
        var amount = Money.Create(50m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Custom; // Not configured
        const string paymentMethodToken = "pm_test_123";

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount,
            methodType,
            provider,
            paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.InvalidProvider");
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenProviderThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var amount = Money.Create(100m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_test_123";

        var exception = new InvalidOperationException("Network error");
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount,
            methodType,
            provider,
            paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Network error");
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithNullMetadata_ShouldProvideEmptyDictionary()
    {
        // Arrange
        var amount = Money.Create(100m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_test_123";

        var expectedResult = new PaymentResult
        {
            TransactionId = "pi_test_123",
            Status = PaymentStatus.Succeeded
        };

        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount,
            methodType,
            provider,
            paymentMethodToken,
            metadata: null);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        _mockStripeProvider.Verify(p => p.ProcessPaymentAsync(
            It.Is<PaymentRequest>(r => r.Metadata != null && r.Metadata.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_WithValidProvider_ShouldReturnSuccess()
    {
        // Arrange
        const string transactionId = "pi_test_123";
        var amount = Money.Create(50m, "USD").Value;
        const string reason = "requested_by_customer";
        const string expectedRefundId = "re_test_123";

        _mockStripeProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedRefundId));

        _mockPayPalProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Not found")));

        // Act
        var result = await _paymentService.RefundPaymentAsync(transactionId, amount, reason);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedRefundId);

        _mockStripeProvider.Verify(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenNoProviderCanHandle_ShouldReturnFailure()
    {
        // Arrange
        const string transactionId = "pi_unknown_123";
        var amount = Money.Create(50m, "USD").Value;
        const string reason = "requested_by_customer";

        var failure = Result.Failure<string>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Transaction not found"));

        _mockStripeProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        _mockPayPalProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        // Act
        var result = await _paymentService.RefundPaymentAsync(transactionId, amount, reason);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("No provider could process the refund");

        _mockStripeProvider.Verify(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()), Times.Once);
        _mockPayPalProvider.Verify(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenProviderThrowsException_ShouldReturnFailure()
    {
        // Arrange
        const string transactionId = "pi_test_123";
        var exception = new InvalidOperationException("Connection timeout");

        _mockStripeProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _paymentService.RefundPaymentAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Connection timeout");
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WithValidProvider_ShouldReturnStatus()
    {
        // Arrange
        const string transactionId = "pi_test_123";
        const PaymentStatus expectedStatus = PaymentStatus.Succeeded;

        _mockStripeProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedStatus));

        _mockPayPalProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentStatus>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Not found")));

        // Act
        var result = await _paymentService.GetPaymentStatusAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedStatus);

        _mockStripeProvider.Verify(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenNoProviderCanHandle_ShouldReturnFailure()
    {
        // Arrange
        const string transactionId = "pi_unknown_123";
        var failure = Result.Failure<PaymentStatus>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Transaction not found"));

        _mockStripeProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        _mockPayPalProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        // Act
        var result = await _paymentService.GetPaymentStatusAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("No provider could get the payment status");

        _mockStripeProvider.Verify(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()), Times.Once);
        _mockPayPalProvider.Verify(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateCustomerAsync_WithValidProvider_ShouldReturnCustomerId()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string userId = "user_123";
        const string email = "test@example.com";
        var metadata = new Dictionary<string, object> { ["plan"] = "premium" };
        const string expectedCustomerId = "cus_test_123";

        _mockStripeProvider
            .Setup(p => p.GetOrCreateCustomerAsync(userId, email, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        // Act
        var result = await _paymentService.GetOrCreateCustomerAsync(provider, userId, email, metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedCustomerId);

        _mockStripeProvider.Verify(p => p.GetOrCreateCustomerAsync(userId, email, metadata, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateCustomerAsync_WithInvalidProvider_ShouldReturnFailure()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Custom; // Not configured
        const string userId = "user_123";
        const string email = "test@example.com";

        // Act
        var result = await _paymentService.GetOrCreateCustomerAsync(provider, userId, email);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.InvalidProvider");
    }

    [Fact]
    public async Task AttachPaymentMethodToCustomerAsync_WithValidProvider_ShouldReturnPaymentMethodId()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_test_123";
        const string customerId = "cus_test_123";
        const string expectedPaymentMethodId = "pm_attached_123";

        _mockStripeProvider
            .Setup(p => p.AttachPaymentMethodToCustomerAsync(paymentMethodToken, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedPaymentMethodId));

        // Act
        var result = await _paymentService.AttachPaymentMethodToCustomerAsync(provider, paymentMethodToken, customerId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedPaymentMethodId);

        _mockStripeProvider.Verify(p => p.AttachPaymentMethodToCustomerAsync(paymentMethodToken, customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithValidProvider_ShouldReturnWebhookResult()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string webhookPayload = """{"id": "evt_test_webhook", "type": "payment_intent.succeeded"}""";
        const string signature = "test_signature";
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" };

        var expectedResult = new WebhookResult
        {
            EventId = "evt_test_webhook",
            EventType = "payment_intent.succeeded",
            ProcessedAt = DateTime.UtcNow,
            IsProcessed = true
        };

        _mockStripeProvider
            .Setup(p => p.ProcessWebhookAsync(webhookPayload, signature, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));

        // Act
        var result = await _paymentService.ProcessWebhookAsync(provider, webhookPayload, signature, headers);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventId.Should().Be("evt_test_webhook");
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.IsProcessed.Should().BeTrue();
        result.Value.Provider.Should().Be(PaymentProvider.Stripe);

        _mockStripeProvider.Verify(p => p.ProcessWebhookAsync(webhookPayload, signature, headers, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WithInvalidProvider_ShouldReturnFailure()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Custom; // Not configured
        const string webhookPayload = """{"id": "evt_test_webhook"}""";

        // Act
        var result = await _paymentService.ProcessWebhookAsync(provider, webhookPayload);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.InvalidProvider");
    }

    [Fact]
    public async Task CreateSetupIntentAsync_WithValidProvider_ShouldReturnSetupIntentResult()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string customerId = "cus_test_123";
        const string paymentMethodToken = "pm_test_123";
        var metadata = new Dictionary<string, object> { ["setup_for"] = "future_payments" };

        var expectedResult = new SetupIntentResult
        {
            SetupIntentId = "seti_test_123",
            Status = PaymentStatus.Succeeded,
            ClientSecret = "seti_test_123_secret",
            CustomerId = customerId
        };

        _mockStripeProvider
            .Setup(p => p.CreateSetupIntentAsync(customerId, paymentMethodToken, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));

        // Act
        var result = await _paymentService.CreateSetupIntentAsync(provider, customerId, paymentMethodToken, metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SetupIntentId.Should().Be("seti_test_123");
        result.Value.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.CustomerId.Should().Be(customerId);

        _mockStripeProvider.Verify(p => p.CreateSetupIntentAsync(customerId, paymentMethodToken, metadata, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmSetupIntentAsync_WithValidProvider_ShouldReturnSetupIntentResult()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string setupIntentId = "seti_test_123";
        const string paymentMethodToken = "pm_test_123";

        var expectedResult = new SetupIntentResult
        {
            SetupIntentId = setupIntentId,
            Status = PaymentStatus.Succeeded,
            PaymentMethodId = "pm_attached_123"
        };

        _mockStripeProvider
            .Setup(p => p.ConfirmSetupIntentAsync(setupIntentId, paymentMethodToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResult));

        // Act
        var result = await _paymentService.ConfirmSetupIntentAsync(provider, setupIntentId, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SetupIntentId.Should().Be(setupIntentId);
        result.Value.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.PaymentMethodId.Should().Be("pm_attached_123");

        _mockStripeProvider.Verify(p => p.ConfirmSetupIntentAsync(setupIntentId, paymentMethodToken, It.IsAny<CancellationToken>()), Times.Once);
    }
}