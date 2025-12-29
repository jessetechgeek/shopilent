using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Payments.Commands.SetDefaultPaymentMethod.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;

namespace Shopilent.Application.UnitTests.Features.Payments.Commands.V1;

public class SetDefaultPaymentMethodCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public SetDefaultPaymentMethodCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<SetDefaultPaymentMethodCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssemblyContaining<SetDefaultPaymentMethodCommandV1>();
        });

        // Register validator
        services.AddTransient<FluentValidation.IValidator<SetDefaultPaymentMethodCommandV1>, SetDefaultPaymentMethodCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var currentDefaultId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .WithDisplayName("Visa ending in 4242")
            .Build(); // IsDefault is false by default

        var currentDefaultMethod = new PaymentMethodBuilder()
            .WithId(currentDefaultId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.PayPal)
            .WithProvider(PaymentProvider.PayPal)
            .IsDefault()
            .Build();

        var userPaymentMethods = new List<PaymentMethod>
        {
            currentDefaultMethod,
            paymentMethod
        };

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(userPaymentMethods);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PaymentMethodId.Should().Be(paymentMethodId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Visa ending in 4242");

        // Verify current default was unset
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(currentDefaultMethod, CancellationToken),
            Times.Once);

        // Verify new default was set
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(paymentMethod, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithNonExistentPaymentMethod_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        // Mock payment method not found
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync((PaymentMethod)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.NotFound(paymentMethodId).Code);

        // Verify no updates occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithPaymentMethodBelongingToDifferentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(otherUserId) // Different user
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .Build();

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.NotFound(paymentMethodId).Code);

        // Verify no updates occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithInactivePaymentMethod_ReturnsInactiveError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .IsInactive() // Inactive payment method
            .Build();

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.InactivePaymentMethod.Code);

        // Verify no updates occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithAlreadyDefaultPaymentMethod_ReturnsSuccessWithoutChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .WithDisplayName("Visa ending in 4242")
            .IsDefault() // Already default
            .Build();

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PaymentMethodId.Should().Be(paymentMethodId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Visa ending in 4242");

        // Verify no updates occurred since it was already default
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.GetByUserIdAsync(It.IsAny<Guid>(), CancellationToken),
            Times.Never);

        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);

        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithNoCurrentDefault_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = paymentMethodId,
            UserId = userId
        };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .WithDisplayName("Visa ending in 4242")
            .Build(); // IsDefault is false by default

        var userPaymentMethods = new List<PaymentMethod>
        {
            paymentMethod
            // No default payment method in the list
        };

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(userPaymentMethods);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PaymentMethodId.Should().Be(paymentMethodId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Visa ending in 4242");

        // Verify only the new default was updated (no current default to unset)
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(paymentMethod, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_WithMultiplePaymentMethods_UnsetsPreviousDefaultCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var newDefaultId = Guid.NewGuid();
        var currentDefaultId = Guid.NewGuid();
        var thirdMethodId = Guid.NewGuid();

        var command = new SetDefaultPaymentMethodCommandV1
        {
            PaymentMethodId = newDefaultId,
            UserId = userId
        };

        var newDefaultMethod = new PaymentMethodBuilder()
            .WithId(newDefaultId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            .WithDisplayName("Visa ending in 4242")
            .Build();

        var currentDefaultMethod = new PaymentMethodBuilder()
            .WithId(currentDefaultId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.PayPal)
            .WithProvider(PaymentProvider.PayPal)
            .WithDisplayName("PayPal Account")
            .IsDefault()
            .Build();

        var thirdMethod = new PaymentMethodBuilder()
            .WithId(thirdMethodId)
            .WithUserId(userId)
            .WithType(PaymentMethodType.BankTransfer)
            .WithProvider(PaymentProvider.Stripe)
            .WithDisplayName("Bank Account")
            .Build();

        var userPaymentMethods = new List<PaymentMethod>
        {
            currentDefaultMethod,
            newDefaultMethod,
            thirdMethod
        };

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(newDefaultId, CancellationToken))
            .ReturnsAsync(newDefaultMethod);

        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(userPaymentMethods);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PaymentMethodId.Should().Be(newDefaultId);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Visa ending in 4242");

        // Verify current default was unset
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(currentDefaultMethod, CancellationToken),
            Times.Once);

        // Verify new default was set
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(newDefaultMethod, CancellationToken),
            Times.Once);

        // Verify third method was not touched
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.UpdateAsync(thirdMethod, CancellationToken),
            Times.Never);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.CommitAsync(CancellationToken),
            Times.Once);
    }
}
