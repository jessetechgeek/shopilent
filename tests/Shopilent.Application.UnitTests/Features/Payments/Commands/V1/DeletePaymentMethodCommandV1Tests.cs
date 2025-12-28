using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Payments.Commands.DeletePaymentMethod.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Application.UnitTests.Testing.Builders;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Errors;

namespace Shopilent.Application.UnitTests.Features.Payments.Commands.V1;

public class DeletePaymentMethodCommandV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public DeletePaymentMethodCommandV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockUnitOfWork.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodWriteRepository.Object);
        services.AddTransient(sp => Fixture.MockPaymentMethodReadRepository.Object);
        services.AddTransient(sp => Fixture.MockCurrentUserContext.Object);
        services.AddTransient(sp => Fixture.GetLogger<DeletePaymentMethodCommandHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DeletePaymentMethodCommandV1>();
        });

        // Register validator
        services
            .AddTransient<FluentValidation.IValidator<DeletePaymentMethodCommandV1>,
                DeletePaymentMethodCommandValidatorV1>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task DeletePaymentMethod_WithValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithToken("pm_test_123456789")
            .WithType(PaymentMethodType.CreditCard)
            .WithProvider(PaymentProvider.Stripe)
            // IsDefault is false by default
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Mock no active payments using this payment method
        Fixture.MockPaymentReadRepository
            .Setup(repo => repo.GetByPaymentMethodIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(new List<PaymentDto>());

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify payment method was deleted
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(paymentMethod, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithUnauthenticatedUser_ReturnsUnauthorizedError()
    {
        // Arrange
        var paymentMethodId = Guid.NewGuid();
        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        // Don't set authenticated user

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.NotFound");

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithNonExistentPaymentMethod_ReturnsNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock payment method not found
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync((PaymentMethod)null);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(PaymentMethodErrors.NotFound(paymentMethodId).Code);

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithPaymentMethodNotOwnedByUser_ReturnsForbiddenError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(otherUserId) // Different user
            .WithToken("pm_test_123456789")
            .Build();

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.NotOwned");

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithPendingPayments_ReturnsConflictError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithToken("pm_test_123456789")
            .Build();

        var pendingPayments = new List<PaymentDto>
        {
            new PaymentDto
            {
                Id = Guid.NewGuid(),
                PaymentMethodId = paymentMethodId,
                Status = PaymentStatus.Pending,
                Amount = 100.00m,
                Currency = "USD"
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        Fixture.MockPaymentReadRepository
            .Setup(repo => repo.GetByPaymentMethodIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(pendingPayments);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.InUse");

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithProcessingPayments_ReturnsConflictError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var paymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithToken("pm_test_123456789")
            .Build();

        var processingPayments = new List<PaymentDto>
        {
            new PaymentDto
            {
                Id = Guid.NewGuid(),
                PaymentMethodId = paymentMethodId,
                Status = PaymentStatus.Processing,
                Amount = 150.00m,
                Currency = "USD"
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(paymentMethod);

        Fixture.MockPaymentReadRepository
            .Setup(repo => repo.GetByPaymentMethodIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(processingPayments);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.InUse");

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithDefaultPaymentMethodAndOtherMethods_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var defaultPaymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithToken("pm_test_123456789")
            .IsDefault()
            .Build();

        var userPaymentMethods = new List<PaymentMethodDto>
        {
            new PaymentMethodDto
            {
                Id = paymentMethodId,
                UserId = userId,
                Type = PaymentMethodType.CreditCard,
                Provider = PaymentProvider.Stripe,
                IsDefault = true
            },
            new PaymentMethodDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = PaymentMethodType.PayPal,
                Provider = PaymentProvider.PayPal,
                IsDefault = false
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(defaultPaymentMethod);

        Fixture.MockPaymentReadRepository
            .Setup(repo => repo.GetByPaymentMethodIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(new List<PaymentDto>());

        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(userPaymentMethods);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethod.IsDefault");

        // Verify no deletion occurred
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(It.IsAny<PaymentMethod>(), CancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task DeletePaymentMethod_WithDefaultPaymentMethodAndNoOtherMethods_ReturnsSuccessfulResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();

        var command = new DeletePaymentMethodCommandV1 { Id = paymentMethodId };

        var defaultPaymentMethod = new PaymentMethodBuilder()
            .WithId(paymentMethodId)
            .WithUserId(userId)
            .WithToken("pm_test_123456789")
            .IsDefault()
            .Build();

        var userPaymentMethods = new List<PaymentMethodDto>
        {
            new PaymentMethodDto
            {
                Id = paymentMethodId,
                UserId = userId,
                Type = PaymentMethodType.CreditCard,
                Provider = PaymentProvider.Stripe,
                IsDefault = true
            }
        };

        // Setup authenticated user
        Fixture.SetAuthenticatedUser(userId);

        // Mock repository calls
        Fixture.MockPaymentMethodWriteRepository
            .Setup(repo => repo.GetByIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(defaultPaymentMethod);

        Fixture.MockPaymentReadRepository
            .Setup(repo => repo.GetByPaymentMethodIdAsync(paymentMethodId, CancellationToken))
            .ReturnsAsync(new List<PaymentDto>());

        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(userPaymentMethods);

        // Act
        var result = await _mediator.Send(command, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify payment method was deleted
        Fixture.MockPaymentMethodWriteRepository.Verify(
            repo => repo.DeleteAsync(defaultPaymentMethod, CancellationToken),
            Times.Once);

        // Verify save was called
        Fixture.MockUnitOfWork.Verify(
            uow => uow.SaveChangesAsync(CancellationToken),
            Times.Once);
    }
}
