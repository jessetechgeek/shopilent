using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Payments.Queries.GetPaymentMethod.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;

namespace Shopilent.Application.UnitTests.Features.Payments.Queries.V1;

public class GetPaymentMethodQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetPaymentMethodQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockPaymentMethodReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetPaymentMethodQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<GetPaymentMethodQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task GetPaymentMethod_WithValidRequest_ReturnsPaymentMethod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = userId,
            Type = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            Token = "pm_test_123456789",
            DisplayName = "Visa ending in 4242",
            CardBrand = "Visa",
            LastFourDigits = "4242",
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(paymentMethodId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Type.Should().Be(PaymentMethodType.CreditCard);
        result.Value.Provider.Should().Be(PaymentProvider.Stripe);
        result.Value.DisplayName.Should().Be("Visa ending in 4242");
        result.Value.CardBrand.Should().Be("Visa");
        result.Value.LastFourDigits.Should().Be("4242");
        result.Value.IsDefault.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_WithNonExistentPaymentMethod_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        // Mock repository calls - payment method not found
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync((PaymentMethodDto)null);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.NotFound(paymentMethodId).Code);

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_WithPaymentMethodBelongingToDifferentUser_ReturnsForbiddenError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = otherUserId, // Belongs to different user
            Type = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            Token = "pm_test_123456789",
            DisplayName = "Visa ending in 4242",
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };

        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.AccessDenied");
        result.Error.Message.Should().Contain("You do not have permission to access this payment method");

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_WithPayPalPaymentMethod_ReturnsCorrectDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = userId,
            Type = PaymentMethodType.PayPal,
            Provider = PaymentProvider.PayPal,
            Token = "ba_test_123456789",
            DisplayName = "PayPal (test@paypal.com)",
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };

        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(paymentMethodId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Type.Should().Be(PaymentMethodType.PayPal);
        result.Value.Provider.Should().Be(PaymentProvider.PayPal);
        result.Value.DisplayName.Should().Be("PayPal (test@paypal.com)");
        result.Value.IsDefault.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_WithBankTransferPaymentMethod_ReturnsCorrectDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = userId,
            Type = PaymentMethodType.BankTransfer,
            Provider = PaymentProvider.Stripe,
            Token = "btxn_test_123456789",
            DisplayName = "Bank Account ****1234",
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };

        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(paymentMethodId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Type.Should().Be(PaymentMethodType.BankTransfer);
        result.Value.Provider.Should().Be(PaymentProvider.Stripe);
        result.Value.DisplayName.Should().Be("Bank Account ****1234");
        result.Value.IsDefault.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_WithInactivePaymentMethod_ReturnsPaymentMethod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodDto
        {
            Id = paymentMethodId,
            UserId = userId,
            Type = PaymentMethodType.CreditCard,
            Provider = PaymentProvider.Stripe,
            Token = "pm_test_123456789",
            DisplayName = "Visa ending in 4242 (Inactive)",
            CardBrand = "Visa",
            LastFourDigits = "4242",
            IsDefault = false,
            IsActive = false, // Inactive payment method
            CreatedAt = DateTime.UtcNow.AddDays(-60),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(paymentMethodId);
        result.Value.UserId.Should().Be(userId);
        result.Value.IsActive.Should().BeFalse();
        result.Value.DisplayName.Should().Contain("Inactive");

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByIdAsync(paymentMethodId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentMethod_HasCorrectCacheConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var query = new GetPaymentMethodQueryV1
        {
            Id = paymentMethodId,
            UserId = userId
        };

        // Assert cache configuration
        query.CacheKey.Should().Be($"payment-method-{paymentMethodId}-user-{userId}");
        query.Expiration.Should().Be(TimeSpan.FromMinutes(15));
        (query is Application.Abstractions.Caching.ICachedQuery<PaymentMethodDto>).Should().BeTrue();
    }
}
