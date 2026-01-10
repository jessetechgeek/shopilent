using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Enums;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Payments.Enums;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Payments.DatabaseIntegration;

[Collection("IntegrationTests")]
public class PaymentWorkflowIntegrationTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserWriteRepository _userWriteRepository = null!;
    private IUserReadRepository _userReadRepository = null!;
    private IOrderWriteRepository _orderWriteRepository = null!;
    private IOrderReadRepository _orderReadRepository = null!;
    private IPaymentWriteRepository _paymentWriteRepository = null!;
    private IPaymentReadRepository _paymentReadRepository = null!;
    private IPaymentMethodWriteRepository _paymentMethodWriteRepository = null!;
    private IPaymentMethodReadRepository _paymentMethodReadRepository = null!;
    private IAddressWriteRepository _addressWriteRepository = null!;

    public PaymentWorkflowIntegrationTests(IntegrationTestFixture integrationTestFixture)
        : base(integrationTestFixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _userWriteRepository = GetService<IUserWriteRepository>();
        _userReadRepository = GetService<IUserReadRepository>();
        _orderWriteRepository = GetService<IOrderWriteRepository>();
        _orderReadRepository = GetService<IOrderReadRepository>();
        _paymentWriteRepository = GetService<IPaymentWriteRepository>();
        _paymentReadRepository = GetService<IPaymentReadRepository>();
        _paymentMethodWriteRepository = GetService<IPaymentMethodWriteRepository>();
        _paymentMethodReadRepository = GetService<IPaymentMethodReadRepository>();
        _addressWriteRepository = GetService<IAddressWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CompletePaymentWorkflow_WithCreditCard_ShouldPersistAllEntities()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create user and order
        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();

        // Create payment method
        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .Build();

        // Create payment with payment method
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m, "USD")
            .WithPaymentMethod(paymentMethod)
            .WithStripeCard()
            .Build();

        // Act - Persist all entities in correct order
        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Assert - Verify all entities are persisted
        var persistedUser = await _userReadRepository.GetByIdAsync(user.Id);
        var persistedOrder = await _orderReadRepository.GetByIdAsync(order.Id);
        var persistedPaymentMethod = await _paymentMethodReadRepository.GetByIdAsync(paymentMethod.Id);
        var persistedPayment = await _paymentReadRepository.GetByIdAsync(payment.Id);

        persistedUser.Should().NotBeNull();
        persistedOrder.Should().NotBeNull();
        persistedPaymentMethod.Should().NotBeNull();
        persistedPayment.Should().NotBeNull();

        // Verify relationships
        persistedPayment.UserId.Should().Be(user.Id);
        persistedPayment.OrderId.Should().Be(order.Id);
        persistedPayment.PaymentMethodId.Should().Be(paymentMethod.Id);
        persistedPayment.Amount.Should().Be(100m);
        persistedPayment.Currency.Should().Be("USD");
        persistedPayment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task PaymentSuccessWorkflow_ShouldUpdateStatusAndPersist()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(50m, "USD")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Act - Mark payment as succeeded
        const string transactionId = "pi_test_succeeded";
        payment.MarkAsSucceeded(transactionId);
        await _paymentWriteRepository.UpdateAsync(payment);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedPayment = await _paymentReadRepository.GetByIdAsync(payment.Id);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Succeeded);
        updatedPayment.TransactionId.Should().Be(transactionId);
        updatedPayment.UpdatedAt.Should().BeAfter(updatedPayment.CreatedAt);

        // Verify transaction ID lookup works
        var paymentByTransaction = await _paymentReadRepository.GetByTransactionIdAsync(transactionId);
        paymentByTransaction.Should().NotBeNull();
        paymentByTransaction!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task PaymentFailureWorkflow_ShouldUpdateStatusWithReason()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();
        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(75m, "USD")
            .WithStripeCard()
            .Build();

        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Act - Mark payment as failed
        const string failureReason = "Your card was declined";
        payment.MarkAsFailed(failureReason);
        await _paymentWriteRepository.UpdateAsync(payment);
        await _unitOfWork.CommitAsync();

        // Assert
        var updatedPayment = await _paymentReadRepository.GetByIdAsync(payment.Id);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Failed);
        updatedPayment.ErrorMessage.Should().Be(failureReason);
        updatedPayment.UpdatedAt.Should().BeAfter(updatedPayment.CreatedAt);
    }

    [Fact]
    public async Task MultiplePaymentsForOrder_ShouldAllBePersisted()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();

        // Create multiple payments for the same order (retry scenario)
        var payment1 = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m, "USD")
            .WithStripeCard()
            .Build();

        var payment2 = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(100m, "USD")
            .WithStripeCard()
            .Build();

        // Mark first payment as failed, second as succeeded
        payment1.MarkAsFailed("Card declined");
        payment2.MarkAsSucceeded("pi_test_retry_success");

        // Act
        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment1);
        await _paymentWriteRepository.AddAsync(payment2);
        await _unitOfWork.CommitAsync();

        // Assert
        var paymentsForOrder = await _paymentReadRepository.GetByOrderIdAsync(order.Id);
        paymentsForOrder.Should().HaveCount(2);

        var failedPayment = paymentsForOrder.First(p => p.Status == PaymentStatus.Failed);
        var successfulPayment = paymentsForOrder.First(p => p.Status == PaymentStatus.Succeeded);

        failedPayment.Should().NotBeNull();
        failedPayment.ErrorMessage.Should().Be("Card declined");

        successfulPayment.Should().NotBeNull();
        successfulPayment.TransactionId.Should().Be("pi_test_retry_success");
    }

    [Fact]
    public async Task PaymentWithPaymentMethod_ShouldMaintainRelationship()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();

        var paymentMethod = PaymentMethodBuilder.Random()
            .WithUser(user)
            .WithCreditCard()
            .WithDisplayName("My Visa Card")
            .Build();

        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(125m, "USD")
            .WithPaymentMethod(paymentMethod)
            .Build();

        // Act
        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _paymentMethodWriteRepository.AddAsync(paymentMethod);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Assert
        var persistedPayment = await _paymentReadRepository.GetByIdAsync(payment.Id);
        persistedPayment.Should().NotBeNull();
        persistedPayment!.PaymentMethodId.Should().Be(paymentMethod.Id);

        // Verify we can find payments by payment method
        var paymentsByMethod = await _paymentReadRepository.GetByPaymentMethodIdAsync(paymentMethod.Id);
        paymentsByMethod.Should().HaveCount(1);
        paymentsByMethod.First().Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task PaymentStatusFiltering_ShouldReturnCorrectPayments()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuilder1 = OrderBuilder.Random().WithUser(user);
        var order1 = orderBuilder1.Build();
        var orderBuilder2 = OrderBuilder.Random().WithUser(user);
        var order2 = orderBuilder2.Build();
        var orderBuilder3 = OrderBuilder.Random().WithUser(user);
        var order3 = orderBuilder3.Build();

        var pendingPayment = PaymentBuilder.Random().WithOrder(order1).WithUser(user).Build();
        var succeededPayment = PaymentBuilder.Random().WithOrder(order2).WithUser(user).Build();
        var failedPayment = PaymentBuilder.Random().WithOrder(order3).WithUser(user).Build();

        succeededPayment.MarkAsSucceeded("pi_test_success");
        failedPayment.MarkAsFailed("Insufficient funds");

        await _userWriteRepository.AddAsync(user);
        await orderBuilder1.PersistAddressesAsync(_addressWriteRepository);
        await orderBuilder2.PersistAddressesAsync(_addressWriteRepository);
        await orderBuilder3.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order1);
        await _orderWriteRepository.AddAsync(order2);
        await _orderWriteRepository.AddAsync(order3);
        await _paymentWriteRepository.AddAsync(pendingPayment);
        await _paymentWriteRepository.AddAsync(succeededPayment);
        await _paymentWriteRepository.AddAsync(failedPayment);
        await _unitOfWork.CommitAsync();

        // Act & Assert
        var pendingPayments = await _paymentReadRepository.GetByStatusAsync(PaymentStatus.Pending);
        pendingPayments.Should().HaveCount(1);
        pendingPayments.First().Id.Should().Be(pendingPayment.Id);

        var succeededPayments = await _paymentReadRepository.GetByStatusAsync(PaymentStatus.Succeeded);
        succeededPayments.Should().HaveCount(1);
        succeededPayments.First().Id.Should().Be(succeededPayment.Id);

        var failedPayments = await _paymentReadRepository.GetByStatusAsync(PaymentStatus.Failed);
        failedPayments.Should().HaveCount(1);
        failedPayments.First().Id.Should().Be(failedPayment.Id);
    }

    [Fact]
    public async Task PaymentExternalReferenceWorkflow_ShouldEnableTracking()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();
        const string externalRef = "stripe_intent_pi_12345";

        var payment = PaymentBuilder.Random()
            .WithOrder(order)
            .WithUser(user)
            .WithAmount(200m, "USD")
            .WithExternalReference(externalRef)
            .WithStripeCard()
            .Build();

        // Act
        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order);
        await _paymentWriteRepository.AddAsync(payment);
        await _unitOfWork.CommitAsync();

        // Assert
        var paymentByReference = await _paymentReadRepository.GetByExternalReferenceAsync(externalRef);
        paymentByReference.Should().NotBeNull();
        paymentByReference!.Id.Should().Be(payment.Id);
        paymentByReference.ExternalReference.Should().Be(externalRef);
        paymentByReference.Provider.Should().Be(PaymentProvider.Stripe);
    }

    [Fact]
    public async Task RecentPaymentsQuery_ShouldReturnInCorrectOrder()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        await _userWriteRepository.AddAsync(user);
        await _unitOfWork.CommitAsync();

        var payments = new List<Domain.Payments.Payment>();

        // Create payments one at a time with individual saves to ensure different timestamps
        for (int i = 0; i < 5; i++)
        {
            var orderBuilder = OrderBuilder.Random().WithUser(user);
            var order = orderBuilder.Build();
            var payment = PaymentBuilder.Random()
                .WithOrder(order)
                .WithUser(user)
                .WithAmount(100m + i * 10, "USD")
                .WithStripeCard()
                .Build();

            payments.Add(payment);
            await orderBuilder.PersistAddressesAsync(_addressWriteRepository);
            await _orderWriteRepository.AddAsync(order);
            await _paymentWriteRepository.AddAsync(payment);
            await _unitOfWork.CommitAsync();

            // Add delay to ensure different creation timestamps
            await Task.Delay(50);
        }

        // Act
        var recentPayments = await _paymentReadRepository.GetRecentPaymentsAsync(3);

        // Assert
        recentPayments.Should().HaveCount(3);

        // Should be ordered by creation time descending (most recent first)
        var orderedPayments = recentPayments.ToList();
        for (int i = 0; i < orderedPayments.Count - 1; i++)
        {
            orderedPayments[i].CreatedAt.Should().BeOnOrAfter(orderedPayments[i + 1].CreatedAt);
        }

        // Most recent payment should be the last one we created
        orderedPayments.First().Amount.Should().Be(140m); // 100 + 4*10
    }

    [Fact]
    public async Task ConcurrentPaymentCreation_ShouldHandleCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = UserBuilder.Random().WithVerifiedEmail().Build();
        var orderBuiilder = OrderBuilder.Random().WithUser(user);
        var order = orderBuiilder.Build();

        await _userWriteRepository.AddAsync(user);
        await orderBuiilder.PersistAddressesAsync(_addressWriteRepository);
        await _orderWriteRepository.AddAsync(order);
        await _unitOfWork.CommitAsync();

        // Act - Create multiple payments concurrently
        var tasks = Enumerable.Range(1, 3).Select(async i =>
        {
            using var scope = ServiceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var paymentWriteRepository = scope.ServiceProvider.GetRequiredService<IPaymentWriteRepository>();

            var payment = PaymentBuilder.Random()
                .WithOrder(order)
                .WithUser(user)
                .WithAmount(i * 25m, "USD")
                .WithStripeCard()
                .Build();

            await paymentWriteRepository.AddAsync(payment);
            await unitOfWork.CommitAsync();

            return payment.Id;
        });

        var paymentIds = await Task.WhenAll(tasks);

        // Assert
        paymentIds.Should().HaveCount(3);
        paymentIds.Should().OnlyHaveUniqueItems();

        var allPayments = await _paymentReadRepository.GetByOrderIdAsync(order.Id);
        allPayments.Should().HaveCount(3);
        allPayments.Select(p => p.Amount).Should().BeEquivalentTo(new[] { 25m, 50m, 75m });
    }
}
