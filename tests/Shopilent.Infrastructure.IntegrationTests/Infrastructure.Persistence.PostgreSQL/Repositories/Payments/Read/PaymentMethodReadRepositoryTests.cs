using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Read;

[Collection("IntegrationTests")]
public class PaymentMethodReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IPaymentMethodWriteRepository _paymentMethodWriteRepository = null!;
    private IPaymentMethodReadRepository _paymentMethodReadRepository = null!;

    public PaymentMethodReadRepositoryTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _paymentMethodWriteRepository = GetService<IPaymentMethodWriteRepository>();
        _paymentMethodReadRepository = GetService<IPaymentMethodReadRepository>();
        return Task.CompletedTask;
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
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetByIdAsync(paymentMethod.Id);

        // Assert
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
    public async Task GetByIdAsync_NonExistentPaymentMethod_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _paymentMethodReadRepository.GetByIdAsync(nonExistentId);

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
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetByUserIdAsync(user1.Id);

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
        var result = await _paymentMethodReadRepository.GetByUserIdAsync(nonExistentUserId);

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
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetDefaultForUserAsync(user.Id);

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
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetDefaultForUserAsync(user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultForUserAsync_NonExistentUser_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _paymentMethodReadRepository.GetDefaultForUserAsync(nonExistentUserId);

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
        await _unitOfWork.SaveChangesAsync();

        // Act
        var creditCardResults =
            await _paymentMethodReadRepository.GetByTypeAsync(user.Id, PaymentMethodType.CreditCard);
        var payPalResults = await _paymentMethodReadRepository.GetByTypeAsync(user.Id, PaymentMethodType.PayPal);

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
    public async Task GetByTypeAsync_NoPaymentMethodsOfType_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var creditCardMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(creditCardMethod);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetByTypeAsync(user.Id, PaymentMethodType.PayPal);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByTokenAsync_ExistingPaymentMethod_ShouldReturnPaymentMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var token = "pm_test_unique_token_123";
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithToken(token)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetByTokenAsync(token);

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
        var nonExistentToken = "pm_nonexistent";

        // Act
        var result = await _paymentMethodReadRepository.GetByTokenAsync(nonExistentToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TokenExistsAsync_ExistingToken_ShouldReturnTrue()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var token = "pm_test_unique_token_456";
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithToken(token)
            .WithCreditCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.TokenExistsAsync(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TokenExistsAsync_NonExistentToken_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentToken = "pm_nonexistent";

        // Act
        var result = await _paymentMethodReadRepository.TokenExistsAsync(nonExistentToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TokenExistsAsync_NullToken_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _paymentMethodReadRepository.TokenExistsAsync(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TokenExistsAsync_EmptyToken_ShouldReturnFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _paymentMethodReadRepository.TokenExistsAsync("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ListAllAsync_ExistingPaymentMethods_ShouldReturnAllPaymentMethods()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user1 = UserBuilder.Random().WithVerifiedEmail().Build();
        var user2 = UserBuilder.Random().WithVerifiedEmail().Build();

        var paymentMethod1 = PaymentMethodBuilder.Random().WithUser(user1).WithCreditCard().Build();
        var paymentMethod2 = PaymentMethodBuilder.Random().WithUser(user1).WithPayPal().Build();
        var paymentMethod3 = PaymentMethodBuilder.Random().WithUser(user2).WithCreditCard().Build();

        await _userWriteRepository.AddAsync(user1);
        await _userWriteRepository.AddAsync(user2);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod1);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod2);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod3);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(pm => pm.Id == paymentMethod1.Id);
        result.Should().Contain(pm => pm.Id == paymentMethod2.Id);
        result.Should().Contain(pm => pm.Id == paymentMethod3.Id);
    }

    [Fact]
    public async Task ListAllAsync_NoPaymentMethods_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _paymentMethodReadRepository.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_InactivePaymentMethods_ShouldStillReturnThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var activePaymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();
        var inactivePaymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithPayPal()
            .Build();

        inactivePaymentMethod.Deactivate();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(activePaymentMethod);
        await _paymentMethodWriteRepository.AddAsync(inactivePaymentMethod);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentMethodReadRepository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(pm => pm.Id == activePaymentMethod.Id && pm.IsActive);
        result.Should().Contain(pm => pm.Id == inactivePaymentMethod.Id && !pm.IsActive);
    }
}
