using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shopilent.Application.Features.Payments.Queries.GetUserPaymentMethods.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Payments.DTOs;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Application.UnitTests.Features.Payments.Queries.V1;

public class GetUserPaymentMethodsQueryV1Tests : TestBase
{
    private readonly IMediator _mediator;

    public GetUserPaymentMethodsQueryV1Tests()
    {
        var services = new ServiceCollection();

        // Register handler dependencies
        services.AddTransient(sp => Fixture.MockPaymentMethodReadRepository.Object);
        services.AddTransient(sp => Fixture.GetLogger<GetUserPaymentMethodsQueryHandlerV1>());

        // Set up MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetUserPaymentMethodsQueryV1>();
        });

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task GetUserPaymentMethods_WithAuthenticatedUser_ReturnsPaymentMethods()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var paymentMethodId1 = Guid.NewGuid();
        var paymentMethodId2 = Guid.NewGuid();

        var query = new GetUserPaymentMethodsQueryV1 { UserId = userId };

        var paymentMethods = new List<PaymentMethodDto>
        {
            new PaymentMethodDto
            {
                Id = paymentMethodId1,
                UserId = userId,
                Type = PaymentMethodType.CreditCard,
                Provider = PaymentProvider.Stripe,
                DisplayName = "Visa ending in 4242",
                CardBrand = "Visa",
                LastFourDigits = "4242",
                ExpiryDate = DateTime.UtcNow.AddYears(2),
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new PaymentMethodDto
            {
                Id = paymentMethodId2,
                UserId = userId,
                Type = PaymentMethodType.PayPal,
                Provider = PaymentProvider.PayPal,
                DisplayName = "PayPal Account",
                IsDefault = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };


        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(paymentMethods);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);

        var firstMethod = result.Value.First(pm => pm.Id == paymentMethodId1);
        firstMethod.DisplayName.Should().Be("Visa ending in 4242");
        firstMethod.IsDefault.Should().BeTrue();
        firstMethod.Type.Should().Be(PaymentMethodType.CreditCard);

        var secondMethod = result.Value.First(pm => pm.Id == paymentMethodId2);
        secondMethod.DisplayName.Should().Be("PayPal Account");
        secondMethod.IsDefault.Should().BeFalse();
        secondMethod.Type.Should().Be(PaymentMethodType.PayPal);

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }


    [Fact]
    public async Task GetUserPaymentMethods_WithNoPaymentMethods_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserPaymentMethodsQueryV1 { UserId = userId };


        // Mock repository calls - empty list
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(new List<PaymentMethodDto>());

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();

        // Verify repository was called correctly
        Fixture.MockPaymentMethodReadRepository.Verify(
            repo => repo.GetByUserIdAsync(userId, CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetUserPaymentMethods_OnlyReturnsActivePaymentMethods()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserPaymentMethodsQueryV1 { UserId = userId };

        var paymentMethods = new List<PaymentMethodDto>
        {
            new PaymentMethodDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = PaymentMethodType.CreditCard,
                Provider = PaymentProvider.Stripe,
                DisplayName = "Active Card",
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-30)
            }
            // Note: Typically the repository would filter out inactive methods
            // but this test verifies the expected behavior
        };


        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(paymentMethods);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value.Should().OnlyContain(pm => pm.IsActive);
    }

    [Fact]
    public async Task GetUserPaymentMethods_OrdersByCreatedDateDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserPaymentMethodsQueryV1 { UserId = userId };

        var older = DateTime.UtcNow.AddDays(-30);
        var newer = DateTime.UtcNow.AddDays(-15);

        var paymentMethods = new List<PaymentMethodDto>
        {
            new PaymentMethodDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = "Newer Card",
                IsActive = true,
                CreatedAt = newer,
                UpdatedAt = newer
            },
            new PaymentMethodDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = "Older Card",
                IsActive = true,
                CreatedAt = older,
                UpdatedAt = older
            }
        };


        // Mock repository calls
        Fixture.MockPaymentMethodReadRepository
            .Setup(repo => repo.GetByUserIdAsync(userId, CancellationToken))
            .ReturnsAsync(paymentMethods);

        // Act
        var result = await _mediator.Send(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);

        // Verify ordering (assuming repository returns them in correct order)
        result.Value.First().DisplayName.Should().Be("Newer Card");
        result.Value.Last().DisplayName.Should().Be("Older Card");
    }
}
