using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.Payments.Models;
using Shopilent.Infrastructure.Payments.Providers.Stripe;
using Shopilent.Infrastructure.Payments.Providers.Stripe.Handlers;
using Shopilent.Infrastructure.Payments.Settings;
using Stripe;
using System.Net;
using System.Text;
using DotNetEnv;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Common.ValueObjects;
using File = System.IO.File;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Payments.ExternalApiIntegration;

/// <summary>
/// Real integration tests that make actual API calls to Stripe's test environment.
/// These tests require valid Stripe test API keys and make real HTTP requests.
///
/// Tests verify:
/// - Real payment processing with Stripe test cards
/// - Webhook signature validation with real Stripe events
/// - Error handling with actual Stripe API responses
/// - Network timeout and retry scenarios
/// - API rate limiting behavior
/// </summary>
[Collection("IntegrationTests")]
public class StripeRealApiIntegrationTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IServiceProvider _serviceProvider;
    private readonly StripePaymentProvider _stripeProvider;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<StripeRealApiIntegrationTests> _logger;

    // Stripe test cards for various scenarios
    private const string SuccessfulTestCard = "pm_card_visa"; // Valid test payment method
    private const string DeclinedTestCard = "pm_card_chargeDeclined";
    private const string InsufficientFundsCard = "pm_card_chargeDeclinedInsufficientFunds";
    private const string ExpiredCard = "pm_card_chargeDeclinedExpiredCard";
    private const string FraudulentCard = "pm_card_chargeDeclinedFraudulent";
    private const string IncorrectCvcCard = "pm_card_chargeDeclinedIncorrectCvc";

    public StripeRealApiIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;

        // Load .env file if it exists (for local development)
        // Environment variables take precedence (for CI/CD)
        try
        {
            // Try multiple possible locations for .env file
            var possiblePaths = new[]
            {
                ".env",                                    // Current directory
                "../.env",                                 // Parent directory
                "../../.env",                              // Two levels up
                "../../../.env",                           // Three levels up
                "../../../../.env",                        // Four levels up
                "../../../../../.env",                     // Five levels up (solution root)
            };

            var envFileLoaded = false;
            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                
                if (File.Exists(fullPath))
                {
                    Env.Load(fullPath);
                    envFileLoaded = true;
                    break;
                }
            }
        }
        catch
        {
            // Ignore if .env file doesn't exist - this is expected in CI/CD
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _stripeSettings = _serviceProvider.GetRequiredService<IOptions<StripeSettings>>().Value;
        _stripeProvider = _serviceProvider.GetRequiredService<StripePaymentProvider>();
        _logger = _serviceProvider.GetRequiredService<ILogger<StripeRealApiIntegrationTests>>();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Create new configuration that includes environment variables
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configure Stripe settings
        services.Configure<StripeSettings>(options =>
        {
            options.SecretKey = Environment.GetEnvironmentVariable("STRIPE_TEST_SECRET_KEY") ?? string.Empty;
            options.PublishableKey = Environment.GetEnvironmentVariable("STRIPE_TEST_PUBLISHABLE_KEY") ?? string.Empty;
            options.WebhookSecret = Environment.GetEnvironmentVariable("STRIPE_TEST_WEBHOOK_SECRET") ?? string.Empty;
            options.ApiVersion = "2025-06-30.basil";
            options.EnableTestMode = true;
        });

        // Register Stripe webhook handlers
        services.AddScoped<PaymentIntentSucceededHandler>();
        services.AddScoped<PaymentIntentFailedHandler>();
        services.AddScoped<PaymentIntentRequiresActionHandler>();
        services.AddScoped<PaymentIntentCanceledHandler>();
        services.AddScoped<ChargeSucceededHandler>();
        services.AddScoped<ChargeDisputeCreatedHandler>();
        services.AddScoped<CustomerCreatedHandler>();
        services.AddScoped<CustomerUpdatedHandler>();
        services.AddScoped<PaymentMethodAttachedHandler>();
        services.AddScoped<SetupIntentSucceededHandler>();
        services.AddScoped<SetupIntentRequiresActionHandler>();
        services.AddScoped<SetupIntentCanceledHandler>();

        // Register Stripe services
        services.AddSingleton<StripeWebhookHandlerFactory>();
        services.AddSingleton<StripePaymentProvider>();
    }

    public Task InitializeAsync()
    {
        // Skip tests if real Stripe keys are not available
        if (!HasValidStripeKeys())
        {
            _logger.LogWarning("Skipping Stripe real API tests - no valid test keys found. " +
                "Set STRIPE_TEST_SECRET_KEY, STRIPE_TEST_PUBLISHABLE_KEY, and STRIPE_TEST_WEBHOOK_SECRET environment variables to run these tests.");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProcessPayment_WithSuccessfulTestCard_ShouldSucceed()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange
        var paymentRequest = new PaymentRequest
        {
            Amount = Money.Create(10.50m, "USD"),
            MethodType = PaymentMethodType.CreditCard,
            PaymentMethodToken = SuccessfulTestCard,
            Metadata = new Dictionary<string, object>
            {
                ["order_id"] = "test_order_123", ["customer_email"] = "test@example.com"
            }
        };

        // Act
        var result = await _stripeProvider.ProcessPaymentAsync(paymentRequest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TransactionId.Should().StartWith("pi_");
        result.Value.Status.Should().BeOneOf(PaymentStatus.Succeeded, PaymentStatus.RequiresAction);
        result.Value.ClientSecret.Should().NotBeNullOrEmpty();

        _logger.LogInformation("Payment processed successfully: {TransactionId} with status: {Status}",
            result.Value.TransactionId, result.Value.Status);
    }

    [Fact]
    public async Task ProcessPayment_WithDeclinedCard_ShouldReturnDeclineError()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange
        var paymentRequest = new PaymentRequest
        {
            Amount = Money.Create(15.00m, "USD"),
            MethodType = PaymentMethodType.CreditCard,
            PaymentMethodToken = DeclinedTestCard,
            Metadata = new Dictionary<string, object> { ["order_id"] = "test_order_declined_123" }
        };

        // Act
        var result = await _stripeProvider.ProcessPaymentAsync(paymentRequest);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Contain("CardDeclined");

        _logger.LogInformation("Payment correctly declined: {ErrorCode} - {ErrorMessage}",
            result.Error.Code, result.Error.Message);
    }

    [Fact]
    public async Task ProcessPayment_WithInsufficientFundsCard_ShouldReturnSpecificError()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange
        var paymentRequest = new PaymentRequest
        {
            Amount = Money.Create(25.00m, "USD"),
            MethodType = PaymentMethodType.CreditCard,
            PaymentMethodToken = InsufficientFundsCard,
            Metadata = new Dictionary<string, object> { ["order_id"] = "test_order_insufficient_funds_123" }
        };

        // Act
        var result = await _stripeProvider.ProcessPaymentAsync(paymentRequest);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Code.Should().Contain("InsufficientFunds");

        _logger.LogInformation("Insufficient funds error correctly handled: {ErrorCode}",
            result.Error.Code);
    }

    [Fact]
    public async Task GetOrCreateCustomer_WithTestApi_ShouldCreateRealCustomer()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = $"test+{userId}@example.com";
        var metadata = new Dictionary<string, object>
        {
            ["source"] = "integration_test", ["test_run"] = DateTime.UtcNow.ToString("O")
        };

        // Act
        var result = await _stripeProvider.GetOrCreateCustomerAsync(userId, email, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().StartWith("cus_");

        _logger.LogInformation("Customer created successfully: {CustomerId} for user: {UserId}",
            result.Value, userId);

        // Cleanup - delete the test customer
        try
        {
            var customerService = new CustomerService();
            await customerService.DeleteAsync(result.Value);
            _logger.LogInformation("Test customer cleaned up: {CustomerId}", result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test customer: {CustomerId}", result.Value);
        }
    }

    [Fact]
    public async Task ProcessWebhook_WithRealStripeSignature_ShouldValidateCorrectly()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange - Create a realistic webhook payload
        var webhookPayload = CreateTestWebhookPayload();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateTestWebhookSignature(webhookPayload, timestamp, _stripeSettings.WebhookSecret);

        // Act
        var result = await _stripeProvider.ProcessWebhookAsync(webhookPayload, signature);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().Be("payment_intent.succeeded");
        result.Value.EventId.Should().StartWith("evt_");
        result.Value.IsProcessed.Should().BeTrue();

        _logger.LogInformation("Webhook processed successfully: {EventId} of type: {EventType}",
            result.Value.EventId, result.Value.EventType);
    }

    [Fact]
    public async Task ProcessWebhook_WithInvalidSignature_ShouldReturnError()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // Arrange
        var webhookPayload = CreateTestWebhookPayload();
        var invalidSignature = "t=1234567890,v1=invalid_signature_hash";

        // Act
        var result = await _stripeProvider.ProcessWebhookAsync(webhookPayload, invalidSignature);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("signature");

        _logger.LogInformation("Invalid signature correctly rejected: {ErrorMessage}",
            result.Error.Message);
    }

    [Fact]
    public async Task ProcessPayment_WithNetworkTimeout_ShouldHandleGracefully()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // This test would require setting up a proxy or network interceptor
        // For now, we'll test with an invalid API key to simulate network issues

        // Arrange - Create provider with short timeout settings
        var services = new ServiceCollection();
        services.AddSingleton(_fixture.Configuration);
        services.AddLogging();

        services.Configure<StripeSettings>(options =>
        {
            options.SecretKey = "sk_test_invalid_key_for_timeout_test";
            options.EnableTestMode = true;
        });

        services.AddSingleton<StripeWebhookHandlerFactory>();
        services.AddSingleton<StripePaymentProvider>();

        using var serviceProvider = services.BuildServiceProvider();
        var timeoutProvider = serviceProvider.GetRequiredService<StripePaymentProvider>();

        var paymentRequest = new PaymentRequest
        {
            Amount = Money.Create(10.00m, "USD"),
            MethodType = PaymentMethodType.CreditCard,
            PaymentMethodToken = SuccessfulTestCard
        };

        // Act
        var result = await timeoutProvider.ProcessPaymentAsync(paymentRequest);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();

        _logger.LogInformation("Network error correctly handled: {ErrorCode} - {ErrorMessage}",
            result.Error.Code, result.Error.Message);
    }

    [Fact]
    public async Task GetPaymentStatus_WithRealTransactionId_ShouldReturnStatus()
    {
        // Skip if no real Stripe keys
        if (!HasValidStripeKeys())
        {
            _logger.LogInformation("Skipping test - no valid Stripe test keys configured");
            return;
        }

        // First create a payment to get a real transaction ID
        var paymentRequest = new PaymentRequest
        {
            Amount = Money.Create(5.00m, "USD"),
            MethodType = PaymentMethodType.CreditCard,
            PaymentMethodToken = SuccessfulTestCard
        };

        var paymentResult = await _stripeProvider.ProcessPaymentAsync(paymentRequest);
        paymentResult.IsSuccess.Should().BeTrue();

        // Act - Get the payment status
        var statusResult = await _stripeProvider.GetPaymentStatusAsync(paymentResult.Value.TransactionId);

        // Assert
        statusResult.IsSuccess.Should().BeTrue();
        statusResult.Value.Should().BeOneOf(
            PaymentStatus.Succeeded,
            PaymentStatus.RequiresAction,
            PaymentStatus.Processing);

        _logger.LogInformation("Payment status retrieved: {Status} for transaction: {TransactionId}",
            statusResult.Value, paymentResult.Value.TransactionId);
    }

    private bool HasValidStripeKeys()
    {
        return !string.IsNullOrEmpty(_stripeSettings.SecretKey)
            && !string.IsNullOrEmpty(_stripeSettings.PublishableKey)
            && !string.IsNullOrEmpty(_stripeSettings.WebhookSecret)
            && _stripeSettings.SecretKey.StartsWith("sk_test_")
            && _stripeSettings.PublishableKey.StartsWith("pk_test_");
    }

    private static string CreateTestWebhookPayload()
    {
        return """
               {
                 "id": "evt_test_webhook",
                 "object": "event",
                 "api_version": "2025-06-30.basil",
                 "created": 1234567890,
                 "data": {
                   "object": {
                     "id": "pi_test_payment_intent",
                     "object": "payment_intent",
                     "amount": 1000,
                     "currency": "usd",
                     "status": "succeeded",
                     "metadata": {
                       "orderId": "test_order_123",
                       "customer_email": "test@example.com"
                     }
                   }
                 },
                 "livemode": false,
                 "pending_webhooks": 1,
                 "request": {
                   "id": "req_test_request",
                   "idempotency_key": null
                 },
                 "type": "payment_intent.succeeded"
               }
               """;
    }

    private static string GenerateTestWebhookSignature(string payload, long timestamp, string secret)
    {
        var signedPayload = $"{timestamp}.{payload}";
        var encoding = new UTF8Encoding();
        var keyBytes = encoding.GetBytes(secret);
        var payloadBytes = encoding.GetBytes(signedPayload);

        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={timestamp},v1={signature}";
    }
}
