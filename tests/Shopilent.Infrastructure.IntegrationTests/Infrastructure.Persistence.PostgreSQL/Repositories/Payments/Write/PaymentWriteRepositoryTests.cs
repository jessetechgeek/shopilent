using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Write;

[Collection("IntegrationTests")]
public class PaymentWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IOrderWriteRepository _orderWriteRepository = null!;
    private IPaymentWriteRepository _paymentWriteRepository = null!;
    private IPaymentMethodWriteRepository _paymentMethodWriteRepository = null!;

    public PaymentWriteRepositoryTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _orderWriteRepository = GetService<IOrderWriteRepository>();
        _paymentWriteRepository = GetService<IPaymentWriteRepository>();
        _paymentMethodWriteRepository = GetService<IPaymentMethodWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidPayment_ShouldPersistPayment()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(Money.Create(150m, "USD").Value)
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);

        // Act
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.OrderId.Should().Be(payment.OrderId);
        result.UserId.Should().Be(payment.UserId);
        result.Amount.Amount.Should().Be(payment.Amount.Amount);
        result.Currency.Should().Be(payment.Currency);
        result.MethodType.Should().Be(payment.MethodType);
        result.Provider.Should().Be(payment.Provider);
        result.Status.Should().Be(PaymentStatus.Pending);
        result.ExternalReference.Should().Be(payment.ExternalReference);
        result.CreatedAt.Should().BeCloseTo(payment.CreatedAt, TimeSpan.FromMilliseconds(100));
        result.UpdatedAt.Should().BeCloseTo(payment.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AddAsync_WithPaymentMethod_ShouldPersistPaymentWithMethod()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(75m)
            .WithPaymentMethod(paymentMethod)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _orderWriteRepository.AddAsync(order);

        // Act
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.PaymentMethodId.Should().Be(paymentMethod.Id);
        result.MethodType.Should().Be(paymentMethod.Type);
        result.Provider.Should().Be(paymentMethod.Provider);
    }

    [Fact]
    public async Task UpdateAsync_ValidPayment_ShouldUpdatePayment()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Detach entity to simulate real-world scenario
        DbContext.Entry(payment).State = EntityState.Detached;

        // Load fresh entity for update
        var existingPayment = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        var transactionId = "txn_updated_123";
        existingPayment!.MarkAsSucceeded(transactionId);

        // Act
        await _paymentWriteRepository.UpdateAsync(existingPayment);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentStatus.Succeeded);
        result.TransactionId.Should().Be(transactionId);
        result.ProcessedAt.Should().NotBeNull();
        result.UpdatedAt.Should().BeAfter(result.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_PaymentStatusChanges_ShouldUpdateCorrectly()
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

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Detach and reload
        DbContext.Entry(payment).State = EntityState.Detached;
        var existingPayment = await _paymentWriteRepository.GetByIdAsync(payment.Id);

        // Test Failed status
        var errorMessage = "Payment declined by bank";
        existingPayment!.MarkAsFailed(errorMessage);

        // Act
        await _paymentWriteRepository.UpdateAsync(existingPayment);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentStatus.Failed);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public async Task DeleteAsync_ExistingPayment_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(200m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _paymentWriteRepository.DeleteAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().BeNull();
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
            .WithAmount(80m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.OrderId.Should().Be(payment.OrderId);
        result.Amount.Amount.Should().Be(payment.Amount.Amount);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentPayment_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _paymentWriteRepository.GetByIdAsync(nonExistentId);

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
            .WithAmount(60m)
            .Build();

        var transactionId = "txn_write_test_123";
        payment.MarkAsSucceeded(transactionId);

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentWriteRepository.GetByTransactionIdAsync(transactionId);

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
        var result = await _paymentWriteRepository.GetByTransactionIdAsync(nonExistentTransactionId);

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
        var externalReference = "ext_ref_write_test";
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithExternalReference(externalReference)
            .WithAmount(90m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentWriteRepository.GetByExternalReferenceAsync(externalReference);

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
        var result = await _paymentWriteRepository.GetByExternalReferenceAsync(nonExistentReference);

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
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment1);
        await _paymentWriteRepository.AddAsync(payment2);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _paymentWriteRepository.GetByOrderIdAsync(order.Id);

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
        var result = await _paymentWriteRepository.GetByOrderIdAsync(nonExistentOrderId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByStatusAsync_ExistingPayments_ShouldReturnFilteredPayments()
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

        succeededPayment.MarkAsSucceeded("txn_success_write");
        failedPayment.MarkAsFailed("Payment failed");

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order1);
        await _orderWriteRepository.AddAsync(order2);
        await _orderWriteRepository.AddAsync(order3);
        await _paymentWriteRepository.AddAsync(pendingPayment);
        await _paymentWriteRepository.AddAsync(succeededPayment);
        await _paymentWriteRepository.AddAsync(failedPayment);
        await _unitOfWork.SaveChangesAsync();

        // Act & Assert - Test Pending payments
        var pendingResults = await _paymentWriteRepository.GetByStatusAsync(PaymentStatus.Pending);
        pendingResults.Should().HaveCount(1);
        pendingResults.First().Id.Should().Be(pendingPayment.Id);
        pendingResults.First().Status.Should().Be(PaymentStatus.Pending);

        // Act & Assert - Test Succeeded payments
        var succeededResults = await _paymentWriteRepository.GetByStatusAsync(PaymentStatus.Succeeded);
        succeededResults.Should().HaveCount(1);
        succeededResults.First().Id.Should().Be(succeededPayment.Id);
        succeededResults.First().Status.Should().Be(PaymentStatus.Succeeded);

        // Act & Assert - Test Failed payments
        var failedResults = await _paymentWriteRepository.GetByStatusAsync(PaymentStatus.Failed);
        failedResults.Should().HaveCount(1);
        failedResults.First().Id.Should().Be(failedPayment.Id);
        failedResults.First().Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task GetByStatusAsync_NoPaymentsWithStatus_ShouldReturnEmpty()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _paymentWriteRepository.GetByStatusAsync(PaymentStatus.Refunded);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OptimisticConcurrencyControl_ConcurrentUpdates_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(120m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // Create two separate scopes to simulate concurrent access
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var paymentWriteRepository1 = scope1.ServiceProvider.GetRequiredService<IPaymentWriteRepository>();
        var paymentWriteRepository2 = scope2.ServiceProvider.GetRequiredService<IPaymentWriteRepository>();


        // Act - Load same payment in both scopes
        var payment1 = await paymentWriteRepository1.GetByIdAsync(payment.Id);
        var payment2 = await paymentWriteRepository2.GetByIdAsync(payment.Id);

        // Modify both entities
        payment1!.MarkAsSucceeded("txn_scope1");
        payment2!.MarkAsFailed("Failed in scope2");

        // First update should succeed
        await paymentWriteRepository1.UpdateAsync(payment1);
        await unitOfWork1.SaveChangesAsync();

        // Second update should handle concurrency properly
        await paymentWriteRepository2.UpdateAsync(payment2);

        // Assert - Second update should throw concurrency exception
        var concurrencyFunc = async () => await unitOfWork2.SaveChangesAsync();
        await concurrencyFunc.Should().ThrowAsync<ConcurrencyConflictException>();

        // Verify final state
        var finalPayment = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        finalPayment.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkOperations_MultiplePayments_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var payments = new List<Payment>();

        for (int i = 0; i < 3; i++)
        {
            var order = OrderBuilder.Random().WithUser(user).Build();
            var payment = PaymentBuilder.Random()
                .WithOrder(order)
                .WithUser(user)
                .WithAmount(100m + (i * 10))
                .Build();

            payments.Add(payment);
            await _orderWriteRepository.AddAsync(order);
        }

        await _userWriteRepository.AddAsync(user);

        // Act - Add multiple payments
        foreach (var payment in payments)
        {
            await _paymentWriteRepository.AddAsync(payment);
        }

        await _unitOfWork.SaveChangesAsync();

        // Assert
        foreach (var payment in payments)
        {
            var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
            result.Should().NotBeNull();
            result!.Id.Should().Be(payment.Id);
        }

        // Test bulk updates
        foreach (var payment in payments)
        {
            var existingPayment = await _paymentWriteRepository.GetByIdAsync(payment.Id);
            existingPayment!.MarkAsSucceeded($"txn_bulk_{payment.Id}");
            await _paymentWriteRepository.UpdateAsync(existingPayment);
        }

        await _unitOfWork.SaveChangesAsync();

        // Verify bulk updates
        var succeededPayments = await _paymentWriteRepository.GetByStatusAsync(PaymentStatus.Succeeded);
        succeededPayments.Should().HaveCount(3);
        succeededPayments.All(p => p.Status == PaymentStatus.Succeeded).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentRefund_WorkFlow_ShouldUpdateCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var order = OrderBuilder.Random().WithUser(user).Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(250m)
            .Build();

        await _userWriteRepository.AddAsync(user);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        // First make it successful
        DbContext.Entry(payment).State = EntityState.Detached;
        var existingPayment = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        existingPayment!.MarkAsSucceeded("txn_for_refund");
        await _paymentWriteRepository.UpdateAsync(existingPayment);
        await _unitOfWork.SaveChangesAsync();

        // Then refund it
        DbContext.Entry(existingPayment).State = EntityState.Detached;
        var paymentToRefund = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        paymentToRefund!.MarkAsRefunded("refund_txn_123");

        // Act
        await _paymentWriteRepository.UpdateAsync(paymentToRefund);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _paymentWriteRepository.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(PaymentStatus.Refunded);
        result.TransactionId.Should().Be("refund_txn_123");
    }
}
