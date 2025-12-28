using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Read;

[Collection("IntegrationTests")]
public class PaymentReadRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;

    public PaymentReadRepositoryTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPayment_ShouldReturnPayment()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(Money.Create(100m, "USD").Value)
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.PaymentWriter.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByIdAsync(payment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.OrderId.Should().Be(payment.OrderId);
        result.UserId.Should().Be(payment.UserId);
        result.Amount.Should().Be(payment.Amount.Amount);
        result.Currency.Should().Be(payment.Currency);
        result.MethodType.Should().Be(payment.MethodType);
        result.Provider.Should().Be(payment.Provider);
        result.Status.Should().Be(payment.Status);
        result.ExternalReference.Should().Be(payment.ExternalReference);
        result.CreatedAt.Should().BeCloseTo(payment.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(payment.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPayment_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTransactionIdAsync_ExistingPayment_ShouldReturnPayment()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(50m)
            .Build();

        var transactionId = "txn_12345";
        payment.MarkAsSucceeded(transactionId);

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.PaymentWriter.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByTransactionIdAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.TransactionId.Should().Be(transactionId);
        result.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task GetByTransactionIdAsync_NonExistentTransaction_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentTransactionId = "txn_nonexistent";

        // Act
        var result = await _unitOfWork.PaymentReader.GetByTransactionIdAsync(nonExistentTransactionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByExternalReferenceAsync_ExistingPayment_ShouldReturnPayment()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var externalReference = "ext_ref_12345";
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithExternalReference(externalReference)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.PaymentWriter.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByExternalReferenceAsync(externalReference);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.ExternalReference.Should().Be(externalReference);
    }

    [Fact]
    public async Task GetByExternalReferenceAsync_NonExistentReference_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentReference = "ext_ref_nonexistent";

        // Act
        var result = await _unitOfWork.PaymentReader.GetByExternalReferenceAsync(nonExistentReference);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOrderIdAsync_ExistingPayments_ShouldReturnPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment1 = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m)
            .WithStripeCard()
            .Build();
        var payment2 = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(50m)
            .WithPayPal()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.PaymentWriter.AddAsync(payment1);
        await _unitOfWork.PaymentWriter.AddAsync(payment2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByOrderIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == payment1.Id);
        result.Should().Contain(p => p.Id == payment2.Id);
        result.All(p => p.OrderId == order.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GetByOrderIdAsync_NonExistentOrder_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentOrderId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByOrderIdAsync(nonExistentOrderId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByStatusAsync_ExistingPayments_ShouldReturnPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order1 = OrderBuilder.Random().WithUser(user).Build();
        var order2 = OrderBuilder.Random().WithUser(user).Build();
        var order3 = OrderBuilder.Random().WithUser(user).Build();

        var pendingPayment = PaymentBuilder.Random().WithOrder(order1).WithUser(user).Build();
        var succeededPayment = PaymentBuilder.Random().WithOrder(order2).WithUser(user).Build();
        var failedPayment = PaymentBuilder.Random().WithOrder(order3).WithUser(user).Build();

        succeededPayment.MarkAsSucceeded("txn_success");
        failedPayment.MarkAsFailed("Payment declined");

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.OrderWriter.AddAsync(order3);
        await _unitOfWork.PaymentWriter.AddAsync(pendingPayment);
        await _unitOfWork.PaymentWriter.AddAsync(succeededPayment);
        await _unitOfWork.PaymentWriter.AddAsync(failedPayment);
        await _unitOfWork.SaveChangesAsync();

        // Act & Assert - Test Pending payments
        var pendingResults = await _unitOfWork.PaymentReader.GetByStatusAsync(PaymentStatus.Pending);
        pendingResults.Should().HaveCount(1);
        pendingResults.First().Id.Should().Be(pendingPayment.Id);

        // Act & Assert - Test Succeeded payments
        var succeededResults = await _unitOfWork.PaymentReader.GetByStatusAsync(PaymentStatus.Succeeded);
        succeededResults.Should().HaveCount(1);
        succeededResults.First().Id.Should().Be(succeededPayment.Id);

        // Act & Assert - Test Failed payments
        var failedResults = await _unitOfWork.PaymentReader.GetByStatusAsync(PaymentStatus.Failed);
        failedResults.Should().HaveCount(1);
        failedResults.First().Id.Should().Be(failedPayment.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_NoPaymentsWithStatus_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByStatusAsync(PaymentStatus.Refunded);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentPaymentsAsync_ExistingPayments_ShouldReturnRecentPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var payments = new List<Domain.Payments.Payment>();

        // Create 5 payments with slight time differences
        for (int i = 0; i < 5; i++)
        {
            var order = OrderBuilder.Random().WithUser(user).Build();
            var payment = PaymentBuilder.Random()
                .WithOrder(order)
                .WithUser(user)
                .WithAmount(100m + i)
                .Build();

            payments.Add(payment);
            await _unitOfWork.OrderWriter.AddAsync(order);
            await Task.Delay(10); // Small delay to ensure different timestamps
        }

        await _userWriteRepository.AddAsync(user);
        foreach (var payment in payments)
        {
            await _unitOfWork.PaymentWriter.AddAsync(payment);
        }
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetRecentPaymentsAsync(3);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);

        // Should be ordered by CreatedAt descending (most recent first)
        var orderedResult = result.ToList();
        for (int i = 0; i < orderedResult.Count - 1; i++)
        {
            orderedResult[i].CreatedAt.Should().BeOnOrAfter(orderedResult[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task GetRecentPaymentsAsync_RequestMoreThanExists_ShouldReturnAllPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random().WithOrder(order).WithUser(user).Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.PaymentWriter.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetRecentPaymentsAsync(10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByPaymentMethodIdAsync_ExistingPayments_ShouldReturnPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        var order1 = OrderBuilder.Random().WithUser(user).Build();
        var order2 = OrderBuilder.Random().WithUser(user).Build();

        var payment1 = PaymentBuilder.Random()
            .WithOrder(order1)
            .WithUser(user)
            .WithPaymentMethod(paymentMethod)
            .Build();
        var payment2 = PaymentBuilder.Random()
            .WithOrder(order2)
            .WithUser(user)
            .WithPaymentMethod(paymentMethod)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.PaymentMethodWriter.AddAsync(paymentMethod);
        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.PaymentWriter.AddAsync(payment1);
        await _unitOfWork.PaymentWriter.AddAsync(payment2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByPaymentMethodIdAsync(paymentMethod.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == payment1.Id);
        result.Should().Contain(p => p.Id == payment2.Id);
        result.All(p => p.PaymentMethodId == paymentMethod.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GetByPaymentMethodIdAsync_NonExistentPaymentMethod_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentPaymentMethodId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.PaymentReader.GetByPaymentMethodIdAsync(nonExistentPaymentMethodId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAllAsync_ExistingPayments_ShouldReturnAllPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order1 = OrderBuilder.Random().WithUser(user).Build();
        var order2 = OrderBuilder.Random().WithUser(user).Build();

        var payment1 = PaymentBuilder.Random().WithOrder(order1).WithUser(user).Build();
        var payment2 = PaymentBuilder.Random().WithOrder(order2).WithUser(user).Build();

        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.PaymentWriter.AddAsync(payment1);
        await _unitOfWork.PaymentWriter.AddAsync(payment2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == payment1.Id);
        result.Should().Contain(p => p.Id == payment2.Id);
    }

    [Fact]
    public async Task ListAllAsync_NoPayments_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _unitOfWork.PaymentReader.ListAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
