using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Payments.Commands.AddPaymentMethod.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;

namespace Shopilent.Application.UnitTests.Features.Payments.Commands.V1;

public class AddPaymentMethodCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public AddPaymentMethodCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockUserWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.MockPaymentService.Object);
        services.AddTransient(sp => Fixture.GetLogger<AddPaymentMethodCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<AddPaymentMethodCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<AddPaymentMethodCommandV1>, AddPaymentMethodCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task AddPaymentMethod_WithValidCreditCard_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new AddPaymentMethodCommandV1
        {
            Type = "CreditCard",
            Provider = "Stripe",
            PaymentMethodToken = "pm_test_123456789",
            DisplayName = "Test Credit Card",
            CardBrand = "Visa",
            LastFourDigits = "4242",
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            IsDefault = false
        };

        var user = new UserBuilder().WithId(userId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock payment method doesn't exist
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByTokenAsync(command.PaymentMethodToken, CancellationToken))
            .ReturnsAsync((PaymentMethod)null);

        // Mock payment service calls
        Fixture.MockPaymentService
            .Setup(service => service.GetOrCreateCustomerAsync(
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                CancellationToken))
            .ReturnsAsync(Result.Success("cus_test_customer"));

        Fixture.MockPaymentService
            .Setup(service => service.AttachPaymentMethodToCustomerAsync(
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                CancellationToken))
            .ReturnsAsync(Result.Success("pm_test_attached"));

        // Capture payment method being added
        PaymentMethod addedPaymentMethod = null;
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.AddAsync(It.IsAny<PaymentMethod>(), CancellationToken))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => addedPaymentMethod = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Type.Should().Be(command.Type);
        result.Value.Provider.Should().Be(command.Provider);
        result.Value.DisplayName.Should().Be("Visa ending in 4242"); // Domain auto-generates display name
        result.Value.CardBrand.Should().Be(command.CardBrand);
        result.Value.LastFourDigits.Should().Be(command.LastFourDigits);
        result.Value.IsDefault.Should().Be(command.IsDefault);

        // Verify payment method was created and added
        addedPaymentMethod.Should().NotBeNull();
        addedPaymentMethod.Token.Should().Be(command.PaymentMethodToken);

        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task AddPaymentMethod_WithUnauthenticatedUser_ReturnsErrorResult()
    {
        // Arrange
        var command = new AddPaymentMethodCommandV1
        {
            Type = "CreditCard",
            Provider = "Stripe",
            PaymentMethodToken = "pm_test_123456789",
            DisplayName = "Test Credit Card"
        };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("User.NotAuthenticated");

        // Verify no payment method was created
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task AddPaymentMethod_WithNonExistentUser_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new AddPaymentMethodCommandV1
        {
            Type = "CreditCard",
            Provider = "Stripe",
            PaymentMethodToken = "pm_test_123456789",
            DisplayName = "Test Credit Card"
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock user not found
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync((Domain.Identity.User)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(UserErrors.NotFound(userId).Code);

        // Verify no payment method was created
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task AddPaymentMethod_WithDuplicateToken_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingPaymentMethodId = Guid.NewGuid();

        var command = new AddPaymentMethodCommandV1
        {
            Type = "CreditCard",
            Provider = "Stripe",
            PaymentMethodToken = "pm_test_123456789",
            DisplayName = "Test Credit Card"
        };

        var user = new UserBuilder().WithId(userId).Build();
        var existingPaymentMethod = new PaymentMethodBuilder()
            .WithId(existingPaymentMethodId)
            .WithUserId(userId)
            .WithToken(command.PaymentMethodToken)
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        // Mock payment method exists with same token
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByTokenAsync(command.PaymentMethodToken, CancellationToken))
            .ReturnsAsync(existingPaymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.DuplicateTokenForUser.Code);

        // Verify no new payment method was created
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.AddAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task AddPaymentMethod_WithInvalidProvider_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new AddPaymentMethodCommandV1
        {
            Type = "CreditCard",
            Provider = "InvalidProvider",
            PaymentMethodToken = "pm_test_123456789",
            DisplayName = "Test Credit Card"
        };

        var user = new UserBuilder().WithId(userId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByTokenAsync(command.PaymentMethodToken, CancellationToken))
            .ReturnsAsync((PaymentMethod)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.InvalidProviderType.Code);
    }

    [Fact]
    public async Task AddPaymentMethod_WithPayPal_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var command = new AddPaymentMethodCommandV1
        {
            Type = "PayPal",
            Provider = "PayPal",
            PaymentMethodToken = "ba_test_123456789",
            DisplayName = "Test PayPal Account",
            Email = "test@paypal.com",
            IsDefault = true
        };

        var user = new UserBuilder().WithId(userId).Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockUserWriteRepository
            .Setup(repo => repo.GetByIdAsync(userId, CancellationToken))
            .ReturnsAsync(user);

        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByTokenAsync(command.PaymentMethodToken, CancellationToken))
            .ReturnsAsync((PaymentMethod)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Type.Should().Be(command.Type);
        result.Value.Provider.Should().Be(command.Provider);
        result.Value.DisplayName.Should().Be("PayPal (test@paypal.com)"); // Domain auto-generates display name
        result.Value.IsDefault.Should().Be(command.IsDefault);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }
}
