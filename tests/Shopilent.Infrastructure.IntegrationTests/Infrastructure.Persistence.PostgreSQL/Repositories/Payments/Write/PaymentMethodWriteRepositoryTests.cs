using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Payments.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Write;

[Collection("IntegrationTests")]
public class PaymentMethodWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IPaymentMethodWriteRepository _paymentMethodWriteRepository = null!;

    public PaymentMethodWriteRepositoryTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _paymentMethodWriteRepository = GetService<IPaymentMethodWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidPaymentMethod_ShouldPersistPaymentMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);

        // Act
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentMethod.Id);
        result.UserId.Should().Be(paymentMethod.UserId);
        result.Type.Should().Be(paymentMethod.Type);
        result.Provider.Should().Be(paymentMethod.Provider);
        result.Token.Should().Be(paymentMethod.Token);
        result.DisplayName.Should().Be(paymentMethod.DisplayName);
        result.CardBrand.Should().Be(paymentMethod.CardBrand);
        result.LastFourDigits.Should().Be(paymentMethod.LastFourDigits);
        result.IsDefault.Should().Be(paymentMethod.IsDefault);
        result.IsActive.Should().Be(paymentMethod.IsActive);
        result.CreatedAt.Should().BeCloseTo(paymentMethod.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(paymentMethod.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AddAsync_PayPalPaymentMethod_ShouldPersistCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var email = "test@paypal.com";
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithPayPal(email)
            .Build();

        await _userWriteRepository.AddAsync(user);

        // Act
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.Type.Should().Be(PaymentMethodType.PayPal);
        result.Provider.Should().Be(PaymentProvider.PayPal);
        result.DisplayName.Should().Contain("PayPal");
        result.DisplayName.Should().Contain(email);
        result.CardBrand.Should().BeNull();
        result.LastFourDigits.Should().BeNull();
        result.Metadata.Should().ContainKey("email");
        result.Metadata["email"].Should().Be(email);
    }

    [Fact]
    public async Task AddAsync_DefaultPaymentMethod_ShouldPersistAsDefault()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .AsDefault()
            .Build();

        await _userWriteRepository.AddAsync(user);

        // Act
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ValidPaymentMethod_ShouldUpdatePaymentMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Detach entity to simulate real-world scenario
        DbContext.Entry(paymentMethod).State = EntityState.Detached;

        // Load fresh entity for update
        var existingPaymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        var newDisplayName = "Updated Card Name";
        existingPaymentMethod!.UpdateDisplayName(newDisplayName);

        // Act
        await _paymentMethodWriteRepository.UpdateAsync(existingPaymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be(newDisplayName);
        result.UpdatedAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_SetAsDefault_ShouldUpdateCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build(); // Not default initially

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Detach and reload
        DbContext.Entry(paymentMethod).State = EntityState.Detached;
        var existingPaymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        existingPaymentMethod!.SetDefault(true);

        // Act
        await _paymentMethodWriteRepository.UpdateAsync(existingPaymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_DeactivatePaymentMethod_ShouldUpdateStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Detach and reload
        DbContext.Entry(paymentMethod).State = EntityState.Detached;
        var existingPaymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        existingPaymentMethod!.Deactivate();

        // Act
        await _paymentMethodWriteRepository.UpdateAsync(existingPaymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UpdateCardDetails_ShouldUpdateCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Prepare new card details
        var newCardDetails = PaymentCardDetails.Create(
            "Mastercard",
            "5678",
            DateTime.UtcNow.AddYears(3)).Value;

        // Detach and reload
        DbContext.Entry(paymentMethod).State = EntityState.Detached;
        var existingPaymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        existingPaymentMethod!.UpdateCardDetails(newCardDetails);

        // Act
        await _paymentMethodWriteRepository.UpdateAsync(existingPaymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().NotBeNull();
        result!.CardBrand.Should().Be("Mastercard");
        result.LastFourDigits.Should().Be("5678");
        result.DisplayName.Should().Contain("Mastercard");
        result.DisplayName.Should().Contain("5678");
        result.ExpiryDate.Should().BeCloseTo(newCardDetails.ExpiryDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeleteAsync_ExistingPaymentMethod_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        await _paymentMethodWriteRepository.DeleteAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Assert
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPaymentMethod_ShouldReturnPaymentMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentMethod.Id);
        result.UserId.Should().Be(paymentMethod.UserId);
        result.Type.Should().Be(paymentMethod.Type);
        result.Provider.Should().Be(paymentMethod.Provider);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPaymentMethod_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _paymentMethodWriteRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingPaymentMethods_ShouldReturnUserPaymentMethods()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = UserBuilder.Random().WithVerifiedEmail().Build();
        var user2 = UserBuilder.Random().WithVerifiedEmail().Build();

        var paymentMethod1 = PaymentMethodBuilder.Random()
            .WithUser(user1)
            .WithCreditCard()
            .Build();
        var paymentMethod2 = PaymentMethodBuilder.Random()
            .WithUser(user1)
            .WithPayPal()
            .Build();
        var paymentMethod3 = PaymentMethodBuilder.Random()
            .WithUser(user2)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod1);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod2);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod3);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.GetByUserIdAsync(user1.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(pm => pm.Id == paymentMethod1.Id);
        result.Should().Contain(pm => pm.Id == paymentMethod2.Id);
        result.Should().NotContain(pm => pm.Id == paymentMethod3.Id);
        result.All(pm => pm.UserId == user1.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserIdAsync_NonExistentUser_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _paymentMethodWriteRepository.GetByUserIdAsync(nonExistentUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefaultForUserAsync_ExistingDefaultPaymentMethod_ShouldReturnDefault()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var defaultPaymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .AsDefault()
            .Build();
        var nonDefaultPaymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithPayPal()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(defaultPaymentMethod);
        await _paymentMethodWriteRepository.AddAsync(nonDefaultPaymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.GetDefaultForUserAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(defaultPaymentMethod.Id);
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultForUserAsync_NoDefaultPaymentMethod_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build(); // Not default

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.GetDefaultForUserAsync(user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_ExistingPaymentMethods_ShouldReturnFilteredByType()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var creditCardMethod1 = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();
        var creditCardMethod2 = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();
        var payPalMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithPayPal()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(creditCardMethod1);
        await _paymentMethodWriteRepository.AddAsync(creditCardMethod2);
        await _paymentMethodWriteRepository.AddAsync(payPalMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var creditCardResults =
            await _paymentMethodWriteRepository.GetByTypeAsync(user.Id, PaymentMethodType.CreditCard);
        var payPalResults = await _paymentMethodWriteRepository.GetByTypeAsync(user.Id, PaymentMethodType.PayPal);

        // Assert
        creditCardResults.Should().NotBeNull();
        creditCardResults.Should().HaveCount(2);
        creditCardResults.Should().Contain(pm => pm.Id == creditCardMethod1.Id);
        creditCardResults.Should().Contain(pm => pm.Id == creditCardMethod2.Id);
        creditCardResults.All(pm => pm.Type == PaymentMethodType.CreditCard).Should().BeTrue();

        payPalResults.Should().NotBeNull();
        payPalResults.Should().HaveCount(1);
        payPalResults.First().Id.Should().Be(payPalMethod.Id);
        payPalResults.First().Type.Should().Be(PaymentMethodType.PayPal);
    }

    [Fact]
    public async Task GetByTokenAsync_ExistingPaymentMethod_ShouldReturnPaymentMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var token = "pm_write_test_unique_token_789";
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithToken(token)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.GetByTokenAsync(token);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(paymentMethod.Id);
        result.Token.Should().Be(token);
    }

    [Fact]
    public async Task GetByTokenAsync_NonExistentToken_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentToken = "pm_nonexistent_write";

        // Act
        var result = await _paymentMethodWriteRepository.GetByTokenAsync(nonExistentToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TokenExistsAsync_ExistingToken_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var token = "pm_write_exists_test_456";
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithToken(token)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Act
        var result = await _paymentMethodWriteRepository.TokenExistsAsync(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TokenExistsAsync_NonExistentToken_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentToken = "pm_write_nonexistent";

        // Act
        var result = await _paymentMethodWriteRepository.TokenExistsAsync(nonExistentToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task OptimisticConcurrencyControl_ConcurrentUpdates_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        // Create two separate scopes to simulate concurrent access
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var paymentMethodRepository1 = scope1.ServiceProvider.GetRequiredService<IPaymentMethodWriteRepository>();
        var paymentMethodRepository2 = scope2.ServiceProvider.GetRequiredService<IPaymentMethodWriteRepository>();

        // Act - Load same payment method in both scopes
        var paymentMethod1 = await paymentMethodRepository1.GetByIdAsync(paymentMethod.Id);
        var paymentMethod2 = await paymentMethodRepository2.GetByIdAsync(paymentMethod.Id);

        // Modify both entities
        paymentMethod1!.UpdateDisplayName("Updated from scope 1");
        paymentMethod2!.UpdateDisplayName("Updated from scope 2");

        // First update should succeed
        await paymentMethodRepository1.UpdateAsync(paymentMethod1);
        await unitOfWork1.CommitAsync();

        // Second update should handle concurrency properly
        await paymentMethodRepository2.UpdateAsync(paymentMethod2);

        // Assert - Second update should throw concurrency exception
        var concurrencyFunc = async () => await unitOfWork2.CommitAsync();
        await concurrencyFunc.Should().ThrowAsync<ConcurrencyConflictException>();

        // Verify final state
        var finalPaymentMethod = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        finalPaymentMethod.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkOperations_MultiplePaymentMethods_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethods = new List<PaymentMethod>
        {
            PaymentMethodBuilder.Random().WithUser(user).WithCreditCard().Build(),
            PaymentMethodBuilder.Random().WithUser(user).WithPayPal().Build(),
            PaymentMethodBuilder.Random().WithUser(user).WithCreditCard().AsDefault().Build()
        };

        await _userWriteRepository.AddAsync(user);

        // Act - Add multiple payment methods
        foreach (var pm in paymentMethods)
        {
            await _paymentMethodWriteRepository.AddAsync(pm);
        }

        await _unitOfWork.CommitAsync();

        // Assert
        foreach (var pm in paymentMethods)
        {
            var result = await _paymentMethodWriteRepository.GetByIdAsync(pm.Id);
            result.Should().NotBeNull();
            result!.Id.Should().Be(pm.Id);
            result.UserId.Should().Be(user.Id);
        }

        // Verify default payment method
        var defaultPm = await _paymentMethodWriteRepository.GetDefaultForUserAsync(user.Id);
        defaultPm.Should().NotBeNull();
        defaultPm!.IsDefault.Should().BeTrue();
        defaultPm.Id.Should().Be(paymentMethods.Last().Id); // The one marked as default

        // Test bulk updates
        foreach (var pm in paymentMethods)
        {
            var existingPm = await _paymentMethodWriteRepository.GetByIdAsync(pm.Id);
            existingPm!.UpdateDisplayName($"Updated {pm.Id}");
            await _paymentMethodWriteRepository.UpdateAsync(existingPm);
        }

        await _unitOfWork.CommitAsync();

        // Verify bulk updates
        var userPaymentMethods = await _paymentMethodWriteRepository.GetByUserIdAsync(user.Id);
        userPaymentMethods.Should().HaveCount(3);
        userPaymentMethods.All(pm => pm.DisplayName.Contains("Updated")).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentMethodLifecycle_CompleteWorkflow_ShouldWorkCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);

        // Act & Assert - Add payment method
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.CommitAsync();

        var created = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        created.Should().NotBeNull();
        created!.IsActive.Should().BeTrue();
        created.IsDefault.Should().BeFalse();

        // Update to make it default
        DbContext.Entry(paymentMethod).State = EntityState.Detached;
        var toUpdate = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        toUpdate!.SetDefault(true);
        await _paymentMethodWriteRepository.UpdateAsync(toUpdate);
        await _unitOfWork.CommitAsync();

        var updated = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        updated.Should().NotBeNull();
        updated!.IsDefault.Should().BeTrue();

        // Deactivate the payment method
        DbContext.Entry(toUpdate).State = EntityState.Detached;
        var toDeactivate = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        toDeactivate!.Deactivate();
        await _paymentMethodWriteRepository.UpdateAsync(toDeactivate);
        await _unitOfWork.CommitAsync();

        var deactivated = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        deactivated.Should().NotBeNull();
        deactivated!.IsActive.Should().BeFalse();
        deactivated.IsDefault.Should().BeTrue(); // Still default but inactive

        // Finally delete
        await _paymentMethodWriteRepository.DeleteAsync(deactivated);
        await _unitOfWork.CommitAsync();

        var deleted = await _paymentMethodWriteRepository.GetByIdAsync(paymentMethod.Id);
        deleted.Should().BeNull();
    }
}
