using Microsoft.Extensions.Logging;
using Moq;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.Payments.Abstractions;
using Shopilent.Infrastructure.Payments.Models;
using Shopilent.Infrastructure.Payments.Services;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Payments.UnitTests.ErrorHandling;

/// <summary>
/// Unit tests for payment error handling scenarios with mocked providers.
/// These tests verify error mapping and handling logic without external dependencies.
/// </summary>
public class StripeErrorHandlingUnitTests
{
    private readonly Mock<IPaymentProvider> _mockStripeProvider;
    private readonly Mock<IPaymentProvider> _mockPayPalProvider;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;

    public StripeErrorHandlingUnitTests()
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
    public async Task ProcessPayment_WithCardDeclinedError_ShouldReturnInsufficientFundsError()
    {
        // Arrange
        var amount = Money.Create(100m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_declined";

        var cardDeclinedError = Domain.Payments.Errors.PaymentErrors.InsufficientFunds;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(cardDeclinedError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.InsufficientFunds");
        result.Error.Message.Should().Contain("insufficient funds");
    }

    [Fact]
    public async Task ProcessPayment_WithExpiredCardError_ShouldReturnExpiredCardError()
    {
        // Arrange
        var amount = Money.Create(75m, "EUR").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_expired";

        var expiredCardError = Domain.Payments.Errors.PaymentErrors.ExpiredCard;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(expiredCardError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ExpiredCard");
        result.Error.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidCardError_ShouldReturnInvalidCardError()
    {
        // Arrange
        var amount = Money.Create(50m, "GBP").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_invalid";

        var invalidCardError = Domain.Payments.Errors.PaymentErrors.InvalidCard;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(invalidCardError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.InvalidCard");
        result.Error.Message.Should().Contain("card details");
    }

    [Fact]
    public async Task ProcessPayment_WithFraudSuspectedError_ShouldReturnFraudError()
    {
        // Arrange
        var amount = Money.Create(1000m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_fraud";

        var fraudError = Domain.Payments.Errors.PaymentErrors.FraudSuspected;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(fraudError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.FraudSuspected");
        result.Error.Message.Should().Contain("fraud");
    }

    [Fact]
    public async Task ProcessPayment_WithAuthenticationRequiredError_ShouldReturnAuthenticationError()
    {
        // Arrange
        var amount = Money.Create(200m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_3ds_required";

        var authError = Domain.Payments.Errors.PaymentErrors.AuthenticationRequired;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(authError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.AuthenticationRequired");
        result.Error.Message.Should().Contain("authentication");
    }

    [Fact]
    public async Task ProcessPayment_WithRiskLevelTooHighError_ShouldReturnRiskError()
    {
        // Arrange
        var amount = Money.Create(5000m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_high_risk";

        var riskError = Domain.Payments.Errors.PaymentErrors.RiskLevelTooHigh;
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(riskError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.RiskLevelTooHigh");
        result.Error.Message.Should().Contain("risk");
    }

    [Fact]
    public async Task ProcessPayment_WithCardDeclinedGenericError_ShouldReturnCardDeclinedError()
    {
        // Arrange
        var amount = Money.Create(120m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_card_declined_generic";

        var declinedError = Domain.Payments.Errors.PaymentErrors.CardDeclined("Transaction declined by issuer");
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(declinedError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.CardDeclined");
        result.Error.Message.Should().Contain("Transaction declined by issuer");
    }

    [Fact]
    public async Task ProcessPayment_WithProcessingFailedError_ShouldReturnProcessingError()
    {
        // Arrange
        var amount = Money.Create(300m, "CAD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_processing_error";

        var processingError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Payment gateway temporarily unavailable");
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(processingError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            amount, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Payment gateway temporarily unavailable");
    }

    [Fact]
    public async Task RefundPayment_WithRefundFailedError_ShouldReturnRefundError()
    {
        // Arrange
        const string transactionId = "pi_test_refund_failed";
        var amount = Money.Create(100m, "USD").Value;
        const string reason = "requested_by_customer";

        var refundError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Refund failed: charge already refunded");
        _mockStripeProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(refundError));

        _mockPayPalProvider
            .Setup(p => p.RefundPaymentAsync(transactionId, amount, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Not found")));

        // Act
        var result = await _paymentService.RefundPaymentAsync(transactionId, amount, reason);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("No provider could process the refund");
    }

    [Fact]
    public async Task GetPaymentStatus_WithNetworkTimeoutError_ShouldReturnTimeoutError()
    {
        // Arrange
        const string transactionId = "pi_test_timeout";
        var timeoutException = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");

        _mockStripeProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(timeoutException);

        _mockPayPalProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentStatus>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Not found")));

        // Act
        var result = await _paymentService.GetPaymentStatusAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("The request was canceled due to the configured HttpClient.Timeout");
    }

    [Fact]
    public async Task GetPaymentStatus_WhenAllProvidersFailGracefully_ShouldReturnNoProviderMessage()
    {
        // Arrange
        const string transactionId = "pi_test_unknown";

        _mockStripeProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentStatus>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Transaction not found")));

        _mockPayPalProvider
            .Setup(p => p.GetPaymentStatusAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentStatus>(Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Payment not found")));

        // Act
        var result = await _paymentService.GetPaymentStatusAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("No provider could get the payment status");
    }

    [Fact]
    public async Task GetOrCreateCustomer_WithDuplicateEmailError_ShouldReturnExistingCustomer()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string userId = "user_duplicate_test";
        const string email = "duplicate@example.com";
        var metadata = new Dictionary<string, object> { ["plan"] = "premium" };

        // Simulate Stripe returning an existing customer ID when duplicate email is detected
        const string existingCustomerId = "cus_existing_123";
        _mockStripeProvider
            .Setup(p => p.GetOrCreateCustomerAsync(userId, email, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(existingCustomerId));

        // Act
        var result = await _paymentService.GetOrCreateCustomerAsync(provider, userId, email, metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingCustomerId);
    }

    [Fact]
    public async Task AttachPaymentMethod_WithAlreadyAttachedError_ShouldReturnError()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_already_attached";
        const string customerId = "cus_test_123";

        var attachmentError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Payment method already attached to customer");
        _mockStripeProvider
            .Setup(p => p.AttachPaymentMethodToCustomerAsync(paymentMethodToken, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(attachmentError));

        // Act
        var result = await _paymentService.AttachPaymentMethodToCustomerAsync(provider, paymentMethodToken, customerId);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Payment method already attached");
    }

    [Fact]
    public async Task ProcessWebhook_WithInvalidSignatureError_ShouldReturnSignatureError()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string webhookPayload = """{"id": "evt_test_webhook", "type": "payment_intent.succeeded"}""";
        const string invalidSignature = "invalid_signature";
        var headers = new Dictionary<string, string> { ["stripe-signature"] = invalidSignature };

        var signatureError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Invalid webhook signature");
        _mockStripeProvider
            .Setup(p => p.ProcessWebhookAsync(webhookPayload, invalidSignature, headers, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WebhookResult>(signatureError));

        // Act
        var result = await _paymentService.ProcessWebhookAsync(provider, webhookPayload, invalidSignature, headers);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Invalid webhook signature");
    }

    [Fact]
    public async Task CreateSetupIntent_WithInvalidCustomerError_ShouldReturnCustomerError()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string invalidCustomerId = "cus_invalid_123";
        const string paymentMethodToken = "pm_test_123";
        var metadata = new Dictionary<string, object> { ["setup_for"] = "future_payments" };

        var customerError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Customer not found");
        _mockStripeProvider
            .Setup(p => p.CreateSetupIntentAsync(invalidCustomerId, paymentMethodToken, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SetupIntentResult>(customerError));

        // Act
        var result = await _paymentService.CreateSetupIntentAsync(provider, invalidCustomerId, paymentMethodToken, metadata);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Customer not found");
    }

    [Fact]
    public async Task ConfirmSetupIntent_WithPaymentMethodMismatchError_ShouldReturnMismatchError()
    {
        // Arrange
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string setupIntentId = "seti_test_123";
        const string wrongPaymentMethodToken = "pm_wrong_123";

        var mismatchError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed("Payment method does not match setup intent");
        _mockStripeProvider
            .Setup(p => p.ConfirmSetupIntentAsync(setupIntentId, wrongPaymentMethodToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SetupIntentResult>(mismatchError));

        // Act
        var result = await _paymentService.ConfirmSetupIntentAsync(provider, setupIntentId, wrongPaymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain("Payment method does not match");
    }

    [Theory]
    [InlineData("USD", 0.01, "Amount too small")]
    [InlineData("EUR", 0.50, "Minimum amount is 0.50 EUR")]
    [InlineData("GBP", 0.30, "Minimum amount is 0.30 GBP")]
    [InlineData("JPY", 50, "Minimum amount is 50 JPY")]
    public async Task ProcessPayment_WithAmountTooSmallError_ShouldReturnAmountError(
        string currency, decimal amount, string expectedMessage)
    {
        // Arrange
        var money = Money.Create(amount, currency).Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;
        const string paymentMethodToken = "pm_amount_too_small";

        var amountError = Domain.Payments.Errors.PaymentErrors.ProcessingFailed(expectedMessage);
        _mockStripeProvider
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PaymentResult>(amountError));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(
            money, methodType, provider, paymentMethodToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Be("Payment.ProcessingFailed");
        result.Error.Message.Should().Contain(expectedMessage);
    }

    [Fact]
    public async Task ProcessPayment_WithMultipleConsecutiveErrors_ShouldTrackErrorHistory()
    {
        // Arrange
        var amount = Money.Create(100m, "USD").Value;
        const PaymentMethodType methodType = PaymentMethodType.CreditCard;
        const PaymentProvider provider = PaymentProvider.Stripe;

        var errors = new[]
        {
            Domain.Payments.Errors.PaymentErrors.InsufficientFunds,
            Domain.Payments.Errors.PaymentErrors.ExpiredCard,
            Domain.Payments.Errors.PaymentErrors.InvalidCard
        };

        // Setup multiple consecutive payment attempts with different errors
        var attempts = new List<Task<Result<PaymentResult>>>();
        
        for (int i = 0; i < errors.Length; i++)
        {
            var error = errors[i];
            var paymentMethodToken = $"pm_error_{i}";
            
            _mockStripeProvider
                .Setup(p => p.ProcessPaymentAsync(
                    It.Is<PaymentRequest>(r => r.PaymentMethodToken == paymentMethodToken), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Failure<PaymentResult>(error));

            // Act
            attempts.Add(_paymentService.ProcessPaymentAsync(
                amount, methodType, provider, paymentMethodToken));
        }

        var results = await Task.WhenAll(attempts);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.IsFailure);
        
        results[0].Error.Code.Should().Be("Payment.InsufficientFunds");
        results[1].Error.Code.Should().Be("Payment.ExpiredCard");
        results[2].Error.Code.Should().Be("Payment.InvalidCard");
    }
}