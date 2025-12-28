using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Payments;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Payments.ValueObjects;

namespace Shopilent.Application.Features.Payments.Commands.AddPaymentMethod.V1;

internal sealed class
    AddPaymentMethodCommandHandlerV1 : ICommandHandler<AddPaymentMethodCommandV1, AddPaymentMethodResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<AddPaymentMethodCommandHandlerV1> _logger;


    public AddPaymentMethodCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        ICurrentUserContext currentUserContext,
        IPaymentService paymentService,
        ILogger<AddPaymentMethodCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _currentUserContext = currentUserContext;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<Result<AddPaymentMethodResponseV1>> Handle(AddPaymentMethodCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current user
            var userId = _currentUserContext.UserId;
            if (!userId.HasValue)
            {
                _logger.LogWarning("User not authenticated");
                return Result.Failure<AddPaymentMethodResponseV1>(Error.Unauthorized(
                    code: "User.NotAuthenticated",
                    message: "User must be authenticated to add payment methods"));
            }

            var user = await _userWriteRepository.GetByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return Result.Failure<AddPaymentMethodResponseV1>(UserErrors.NotFound(userId.Value));
            }

            // Parse provider enum for 3DS setup intent handling
            if (!Enum.TryParse<PaymentProvider>(request.Provider, true, out var provider))
            {
                return Result.Failure<AddPaymentMethodResponseV1>(PaymentMethodErrors.InvalidProviderType);
            }

            // Handle 3DS Setup Intent Flow
            if (request.RequiresSetupIntent && string.IsNullOrEmpty(request.SetupIntentId))
            {
                return await HandleSetupIntentCreationAsync(request, user, provider, cancellationToken);
            }

            // Handle Setup Intent Confirmation Flow
            if (!string.IsNullOrEmpty(request.SetupIntentId))
            {
                return await HandleSetupIntentConfirmationAsync(request, user, provider, cancellationToken);
            }

            // Check if token already exists for this user
            var existingPaymentMethod =
                await _unitOfWork.PaymentMethodWriter.GetByTokenAsync(request.PaymentMethodToken, cancellationToken);
            if (existingPaymentMethod != null && existingPaymentMethod.UserId == userId)
            {
                _logger.LogWarning("Payment method with token already exists for user: {UserId}", userId);
                return Result.Failure<AddPaymentMethodResponseV1>(PaymentMethodErrors.DuplicateTokenForUser);
            }

            // Parse method type enum
            if (!Enum.TryParse<PaymentMethodType>(request.Type, true, out var methodType))
            {
                return Result.Failure<AddPaymentMethodResponseV1>(PaymentMethodErrors.InvalidProviderType);
            }

            // Create payment method based on type
            Result<PaymentMethod> paymentMethodResult = methodType switch
            {
                PaymentMethodType.CreditCard => CreateCreditCardPaymentMethod(user, request, provider),
                PaymentMethodType.PayPal => CreatePayPalPaymentMethod(user, request, provider),
                _ => Result.Failure<PaymentMethod>(PaymentMethodErrors.InvalidProviderType)
            };

            if (paymentMethodResult.IsFailure)
            {
                _logger.LogWarning("Failed to create payment method: {Error}", paymentMethodResult.Error);
                return Result.Failure<AddPaymentMethodResponseV1>(paymentMethodResult.Error);
            }

            var paymentMethod = paymentMethodResult.Value;

            // Add any additional metadata
            foreach (var metadata in request.Metadata ?? new Dictionary<string, object>())
            {
                paymentMethod.UpdateMetadata(metadata.Key, metadata.Value);
            }

            // Handle customer management for providers that support it
            var customerManagementResult =
                await HandleCustomerManagementAsync(user, paymentMethod, provider, false, cancellationToken);
            if (customerManagementResult.IsFailure)
            {
                _logger.LogWarning("Customer management failed: {Error}", customerManagementResult.Error);
                return Result.Failure<AddPaymentMethodResponseV1>(customerManagementResult.Error);
            }

            // Add to repository and save using Unit of Work
            await _unitOfWork.PaymentMethodWriter.AddAsync(paymentMethod, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment method created successfully: {PaymentMethodId} for user: {UserId}",
                paymentMethod.Id, userId);

            // Map to response
            var response = new AddPaymentMethodResponseV1
            {
                Id = paymentMethod.Id,
                Type = paymentMethod.Type.ToString(),
                Provider = paymentMethod.Provider.ToString(),
                DisplayName = paymentMethod.DisplayName,
                CardBrand = paymentMethod.CardBrand,
                LastFourDigits = paymentMethod.LastFourDigits,
                ExpiryDate = paymentMethod.ExpiryDate,
                IsDefault = paymentMethod.IsDefault,
                IsActive = paymentMethod.IsActive,
                Metadata = paymentMethod.Metadata,
                CreatedAt = paymentMethod.CreatedAt
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment method for user: {UserId}", _currentUserContext.UserId);
            return Result.Failure<AddPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.CreationFailed",
                    message: $"Failed to create payment method: {ex.Message}"));
        }
    }

    private async Task<Result> HandleCustomerManagementAsync(
        Domain.Identity.User user,
        PaymentMethod paymentMethod,
        PaymentProvider provider,
        bool skipAttachment = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Only handle customer management for Stripe (for now)
            if (provider != PaymentProvider.Stripe)
            {
                return Result.Success();
            }

            var customerIdMetadataKey = "stripe_customer_id";

            // Always use Stripe as source of truth with idempotency keys to prevent race conditions
            _logger.LogInformation("Getting or creating customer for user {UserId} with provider {Provider}",
                user.Id, provider);

            var createCustomerResult = await _paymentService.GetOrCreateCustomerAsync(
                provider,
                user.Id.ToString(),
                user.Email.Value,
                new Dictionary<string, object>
                {
                    ["full_name"] = $"{user.FullName.FirstName} {user.FullName.LastName}"
                },
                cancellationToken);

            if (createCustomerResult.IsFailure)
            {
                _logger.LogError("Failed to get or create customer: {Error}", createCustomerResult.Error);
                return Result.Failure(createCustomerResult.Error);
            }

            var customerId = createCustomerResult.Value;
            _logger.LogInformation("Customer resolved: {CustomerId} for user {UserId}", customerId, user.Id);

            // Store customer ID in payment method metadata
            paymentMethod.UpdateMetadata(customerIdMetadataKey, customerId);

            // Attach payment method to customer (skip if from setup intent as Stripe handles this automatically)
            if (!skipAttachment)
            {
                var attachResult = await _paymentService.AttachPaymentMethodToCustomerAsync(
                    provider,
                    paymentMethod.Token,
                    customerId,
                    cancellationToken);

                if (attachResult.IsFailure)
                {
                    var errorMessage = attachResult.Error.Message;
                    if (errorMessage.Contains("already been attached") || errorMessage.Contains("already attached"))
                    {
                        _logger.LogInformation(
                            "Payment method {PaymentMethodToken} already attached to customer {CustomerId} - this is expected for setup intent flow",
                            paymentMethod.Token, customerId);
                    }
                    else
                    {
                        _logger.LogError("Failed to attach payment method to customer: {Error}", attachResult.Error);
                        return Result.Failure(attachResult.Error);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Successfully attached payment method {PaymentMethodId} to customer {CustomerId}",
                        paymentMethod.Id, customerId);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Skipping payment method attachment for setup intent flow - Stripe handles this automatically");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling customer management for payment method");
            return Result.Failure(
                Error.Failure(
                    code: "PaymentMethod.CustomerManagementFailed",
                    message: $"Failed to handle customer management: {ex.Message}"));
        }
    }

    private static Result<PaymentMethod> CreateCreditCardPaymentMethod(
        Domain.Identity.User user,
        AddPaymentMethodCommandV1 request,
        PaymentProvider provider)
    {
        // Create card details value object
        var cardDetailsResult = PaymentCardDetails.Create(
            request.CardBrand,
            request.LastFourDigits,
            request.ExpiryDate ?? DateTime.UtcNow.AddYears(1));

        if (cardDetailsResult.IsFailure)
        {
            return Result.Failure<PaymentMethod>(cardDetailsResult.Error);
        }

        // Use the correct factory method
        return PaymentMethod.CreateCardMethod(
            user,
            provider,
            request.PaymentMethodToken,
            cardDetailsResult.Value,
            request.IsDefault);
    }

    private static Result<PaymentMethod> CreatePayPalPaymentMethod(
        Domain.Identity.User user,
        AddPaymentMethodCommandV1 request,
        PaymentProvider provider)
    {
        return PaymentMethod.CreatePayPalMethod(
            user,
            request.PaymentMethodToken,
            request.Email,
            request.IsDefault);
    }

    private async Task<Result<AddPaymentMethodResponseV1>> HandleSetupIntentCreationAsync(
        AddPaymentMethodCommandV1 request,
        Domain.Identity.User user,
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating setup intent for 3DS authentication for user {UserId}", user.Id);

            // Get or create customer ID for the provider using idempotency approach
            var customerIdResult = await _paymentService.GetOrCreateCustomerAsync(
                provider,
                user.Id.ToString(),
                user.Email.Value,
                new Dictionary<string, object>
                {
                    ["full_name"] = $"{user.FullName.FirstName} {user.FullName.LastName}"
                },
                cancellationToken);

            if (customerIdResult.IsFailure)
            {
                return Result.Failure<AddPaymentMethodResponseV1>(customerIdResult.Error);
            }

            var customerId = customerIdResult.Value;

            // Create setup intent with metadata
            var metadata = new Dictionary<string, object>(request.Metadata ?? new Dictionary<string, object>())
            {
                ["user_id"] = user.Id.ToString(),
                ["display_name"] = request.DisplayName ?? "Credit Card",
                ["is_default"] = request.IsDefault.ToString(),
                ["card_brand"] = request.CardBrand ?? "unknown",
                ["last_four_digits"] = request.LastFourDigits ?? "0000"
            };

            var setupIntentResult = await _paymentService.CreateSetupIntentAsync(
                provider,
                customerId,
                request.PaymentMethodToken,
                metadata,
                cancellationToken);

            if (setupIntentResult.IsFailure)
            {
                _logger.LogError("Failed to create setup intent: {Error}", setupIntentResult.Error);
                return Result.Failure<AddPaymentMethodResponseV1>(setupIntentResult.Error);
            }

            var setupIntent = setupIntentResult.Value;

            if (setupIntent.Status == PaymentStatus.Succeeded)
            {
                //create user payment method
                var paymentMethodResult =
                    await CreatePaymentMethodFromConfirmedSetupIntentAsync(request, user, provider, cancellationToken);
                if (paymentMethodResult.IsFailure)
                {
                    _logger.LogWarning("Failed to create payment method from confirmed setup intent: {Error}",
                        paymentMethodResult.Error);
                    return Result.Failure<AddPaymentMethodResponseV1>(paymentMethodResult.Error);
                }
            }

            _logger.LogInformation("Setup intent created successfully: {SetupIntentId} for user {UserId}",
                setupIntent.SetupIntentId, user.Id);

            // Return response indicating 3DS authentication is required
            return Result.Success(new AddPaymentMethodResponseV1
            {
                RequiresAuthentication = setupIntent.RequiresAction,
                SetupIntentId = setupIntent.SetupIntentId,
                ClientSecret = setupIntent.ClientSecret,
                NextActionType = setupIntent.NextActionType,
                PaymentMethodToken = setupIntent.PaymentMethodId,
                Type = request.Type,
                Provider = request.Provider,
                DisplayName = request.DisplayName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating setup intent for user {UserId}", user.Id);
            return Result.Failure<AddPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.SetupIntentCreationFailed",
                    message: $"Failed to create setup intent: {ex.Message}"));
        }
    }

    private async Task<Result<AddPaymentMethodResponseV1>> HandleSetupIntentConfirmationAsync(
        AddPaymentMethodCommandV1 request,
        Domain.Identity.User user,
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing setup intent {SetupIntentId} for user {UserId}",
                request.SetupIntentId, user.Id);

            // First, try to confirm the setup intent
            var confirmResult = await _paymentService.ConfirmSetupIntentAsync(
                provider,
                request.SetupIntentId,
                request.PaymentMethodToken,
                cancellationToken);

            // If confirmation failed because setup intent already succeeded, that's OK
            if (confirmResult.IsFailure)
            {
                var errorMessage = confirmResult.Error.Message;
                if (errorMessage.Contains("already succeeded") ||
                    errorMessage.Contains("setup_intent_unexpected_state"))
                {
                    _logger.LogInformation(
                        "Setup intent {SetupIntentId} already succeeded, proceeding with payment method creation",
                        request.SetupIntentId);

                    var mockResult = new SetupIntentResult
                    {
                        SetupIntentId = request.SetupIntentId,
                        Status = PaymentStatus.Succeeded,
                        PaymentMethodId = request.PaymentMethodToken,
                        RequiresAction = false
                    };

                    return await ProcessSuccessfulSetupIntent(mockResult, request, user, provider, cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to confirm setup intent {SetupIntentId}: {Error}",
                        request.SetupIntentId, confirmResult.Error);
                    return Result.Failure<AddPaymentMethodResponseV1>(confirmResult.Error);
                }
            }

            var setupIntentResult = confirmResult.Value;

            return await ProcessSuccessfulSetupIntent(setupIntentResult, request, user, provider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming setup intent {SetupIntentId} for user {UserId}",
                request.SetupIntentId, user.Id);
            return Result.Failure<AddPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.SetupIntentConfirmationFailed",
                    message: $"Failed to confirm setup intent: {ex.Message}"));
        }
    }

    private async Task<Result<AddPaymentMethodResponseV1>> ProcessSuccessfulSetupIntent(
        SetupIntentResult setupIntentResult,
        AddPaymentMethodCommandV1 request,
        Domain.Identity.User user,
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if the setup intent was successful
            if (setupIntentResult.Status != PaymentStatus.Succeeded)
            {
                _logger.LogWarning("Setup intent {SetupIntentId} was not successful. Status: {Status}",
                    request.SetupIntentId, setupIntentResult.Status);

                // If still requires action, return the authentication requirements
                if (setupIntentResult.RequiresAction)
                {
                    return Result.Success(new AddPaymentMethodResponseV1
                    {
                        RequiresAuthentication = true,
                        SetupIntentId = setupIntentResult.SetupIntentId,
                        ClientSecret = setupIntentResult.ClientSecret,
                        NextActionType = setupIntentResult.NextActionType,
                        PaymentMethodToken = setupIntentResult.PaymentMethodId,
                        Type = request.Type,
                        Provider = request.Provider,
                        DisplayName = request.DisplayName
                    });
                }

                return Result.Failure<AddPaymentMethodResponseV1>(
                    Error.Failure(
                        code: "PaymentMethod.SetupIntentNotSuccessful",
                        message: $"Setup intent confirmation failed with status: {setupIntentResult.Status}"));
            }

            // Extract payment method token from the confirmed setup intent
            var paymentMethodToken = setupIntentResult.PaymentMethodId;
            if (string.IsNullOrEmpty(paymentMethodToken))
            {
                _logger.LogError("No payment method ID returned from confirmed setup intent {SetupIntentId}",
                    request.SetupIntentId);
                return Result.Failure<AddPaymentMethodResponseV1>(
                    Error.Failure(
                        code: "PaymentMethod.MissingPaymentMethodToken",
                        message: "No payment method token returned from setup intent confirmation"));
            }

            // Create a new command with the confirmed payment method token
            var confirmedRequest = request with
            {
                PaymentMethodToken = paymentMethodToken,
                RequiresSetupIntent = false,
                SetupIntentId = null,
                // Merge setup intent metadata with request metadata
                Metadata = MergeMetadata(request.Metadata, setupIntentResult.Metadata)
            };

            _logger.LogInformation(
                "Setup intent {SetupIntentId} processed successfully, creating payment method with token {PaymentMethodToken}",
                request.SetupIntentId, paymentMethodToken);

            // Now proceed with the regular payment method creation flow
            return await CreatePaymentMethodFromConfirmedSetupIntentAsync(confirmedRequest, user, provider,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing successful setup intent {SetupIntentId} for user {UserId}",
                request.SetupIntentId, user.Id);
            return Result.Failure<AddPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.ProcessSetupIntentFailed",
                    message: $"Failed to process setup intent: {ex.Message}"));
        }
    }

    private async Task<Result<AddPaymentMethodResponseV1>> CreatePaymentMethodFromConfirmedSetupIntentAsync(
        AddPaymentMethodCommandV1 request,
        Domain.Identity.User user,
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if token already exists for this user
            var existingPaymentMethod =
                await _unitOfWork.PaymentMethodWriter.GetByTokenAsync(request.PaymentMethodToken, cancellationToken);
            if (existingPaymentMethod != null && existingPaymentMethod.UserId == user.Id)
            {
                _logger.LogWarning("Payment method with token already exists for user: {UserId}", user.Id);
                return Result.Failure<AddPaymentMethodResponseV1>(PaymentMethodErrors.DuplicateTokenForUser);
            }

            // Parse method type enum
            if (!Enum.TryParse<PaymentMethodType>(request.Type, true, out var methodType))
            {
                return Result.Failure<AddPaymentMethodResponseV1>(PaymentMethodErrors.InvalidProviderType);
            }

            // Create payment method based on type
            Result<PaymentMethod> paymentMethodResult = methodType switch
            {
                PaymentMethodType.CreditCard => CreateCreditCardPaymentMethod(user, request, provider),
                PaymentMethodType.PayPal => CreatePayPalPaymentMethod(user, request, provider),
                _ => Result.Failure<PaymentMethod>(PaymentMethodErrors.InvalidProviderType)
            };

            if (paymentMethodResult.IsFailure)
            {
                _logger.LogWarning("Failed to create payment method: {Error}", paymentMethodResult.Error);
                return Result.Failure<AddPaymentMethodResponseV1>(paymentMethodResult.Error);
            }

            var paymentMethod = paymentMethodResult.Value;

            // Add any additional metadata
            foreach (var metadata in request.Metadata ?? new Dictionary<string, object>())
            {
                paymentMethod.UpdateMetadata(metadata.Key, metadata.Value);
            }

            // Handle customer management for providers that support it
            // Note: Skip attachment if this came from a setup intent, as Stripe handles this automatically
            var customerManagementResult =
                await HandleCustomerManagementAsync(user, paymentMethod, provider, true, cancellationToken);
            if (customerManagementResult.IsFailure)
            {
                _logger.LogWarning("Customer management failed: {Error}", customerManagementResult.Error);
                return Result.Failure<AddPaymentMethodResponseV1>(customerManagementResult.Error);
            }

            // Add to repository and save using Unit of Work
            await _unitOfWork.PaymentMethodWriter.AddAsync(paymentMethod, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Payment method created successfully from confirmed setup intent: {PaymentMethodId} for user: {UserId}",
                paymentMethod.Id, user.Id);

            // Map to response
            var response = new AddPaymentMethodResponseV1
            {
                Id = paymentMethod.Id,
                Type = paymentMethod.Type.ToString(),
                Provider = paymentMethod.Provider.ToString(),
                DisplayName = paymentMethod.DisplayName,
                CardBrand = paymentMethod.CardBrand,
                LastFourDigits = paymentMethod.LastFourDigits,
                ExpiryDate = paymentMethod.ExpiryDate,
                IsDefault = paymentMethod.IsDefault,
                IsActive = paymentMethod.IsActive,
                Metadata = paymentMethod.Metadata,
                CreatedAt = paymentMethod.CreatedAt
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment method from confirmed setup intent for user: {UserId}",
                user.Id);
            return Result.Failure<AddPaymentMethodResponseV1>(
                Error.Failure(
                    code: "PaymentMethod.CreationFromSetupIntentFailed",
                    message: $"Failed to create payment method from setup intent: {ex.Message}"));
        }
    }

    private static Dictionary<string, object> MergeMetadata(
        Dictionary<string, object> requestMetadata,
        Dictionary<string, object> setupIntentMetadata)
    {
        var merged = new Dictionary<string, object>(requestMetadata ?? new Dictionary<string, object>());

        if (setupIntentMetadata != null)
        {
            foreach (var kvp in setupIntentMetadata)
            {
                // Prefer setup intent metadata over request metadata for conflicts
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }


}
