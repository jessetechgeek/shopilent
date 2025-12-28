using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Exceptions;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Infrastructure.IntegrationTests.Common;
using Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

namespace Shopilent.Infrastructure.IntegrationTests.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Write;

[Collection("IntegrationTests")]
public class OrderWriteRepositoryTests : IntegrationTestBase
{
    private IUnitOfWork _unitOfWork = null!;
    private ICategoryWriteRepository _categoryWriteRepository = null!;

    public OrderWriteRepositoryTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    protected override Task InitializeTestServices()
    {
        _unitOfWork = GetService<IUnitOfWork>();
        _categoryWriteRepository = GetService<ICategoryWriteRepository>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidOrder_ShouldPersistToDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .WithSubtotal(99.99m)
            .WithTax(8.99m)
            .WithShippingCost(9.99m)
            .WithShippingMethod("Standard Shipping")
            .Build();

        // Act
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.OrderReader.GetByIdAsync(order.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.UserId.Should().Be(user.Id);
        result.Status.Should().Be(OrderStatus.Pending);
        result.Subtotal.Should().Be(99.99m);
        result.Tax.Should().Be(8.99m);
        result.ShippingCost.Should().Be(9.99m);
        result.Total.Should().Be(118.97m); // 99.99 + 8.99 + 9.99
        result.CreatedAt.Should().BeCloseTo(order.CreatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AddAsync_PaidOrder_ShouldPersistWithCorrectStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .AsPaidOrder()
            .Build();

        // Act
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.OrderReader.GetByIdAsync(order.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(OrderStatus.Processing);
        result.PaymentStatus.Should().Be(order.PaymentStatus);
    }

    [Fact]
    public async Task AddAsync_OrderWithItems_ShouldPersistOrderAndItems()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product1 = new ProductBuilder().WithCategory(category).Build();
        var product2 = new ProductBuilder().WithCategory(category).Build();
        await _unitOfWork.ProductWriter.AddAsync(product1);
        await _unitOfWork.ProductWriter.AddAsync(product2);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        // Add items to order
        var unitPrice1 = Money.Create(19.99m, "USD").Value;
        var unitPrice2 = Money.Create(29.99m, "USD").Value;
        order.AddItem(product1, 2, unitPrice1);
        order.AddItem(product2, 1, unitPrice2);

        // Act
        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(item => item.Quantity > 0);
        result.Items.Sum(i => i.TotalPrice).Should().Be(69.97m); // (19.99*2) + (29.99*1)
    }

    [Fact]
    public async Task UpdateAsync_ExistingOrder_ShouldModifyOrder()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and update
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        existingOrder!.UpdateMetadata("notes", "Test order update");
        existingOrder.UpdateOrderStatus(OrderStatus.Processing);

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Processing);
        updatedOrder.Metadata.Should().ContainKey("notes");
        updatedOrder.Metadata["notes"].ToString().Should().Be("Test order update");
        updatedOrder.UpdatedAt.Should().BeAfter(order.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_MarkOrderAsPaid_ShouldUpdateStatusAndPaymentStatus()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and mark as paid
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        existingOrder!.MarkAsPaid();

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Processing);
        updatedOrder.PaymentStatus.Should().Be(existingOrder.PaymentStatus);
    }

    [Fact]
    public async Task UpdateAsync_MarkOrderAsShipped_ShouldUpdateStatusWithTrackingNumber()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .AsPaidOrder()
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and mark as shipped
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        var trackingNumber = "TRACK123456789";
        existingOrder!.MarkAsShipped(trackingNumber);

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Shipped);
        updatedOrder.Metadata.Should().ContainKey("trackingNumber");
        updatedOrder.Metadata["trackingNumber"].ToString().Should().Be(trackingNumber);
    }

    [Fact]
    public async Task UpdateAsync_CancelOrder_ShouldUpdateStatusWithReason()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and cancel
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        var cancellationReason = "Customer requested cancellation";
        existingOrder!.Cancel(cancellationReason, true); // Use admin privileges for test

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Cancelled);
        updatedOrder.Metadata.Should().ContainKey("cancellationReason");
        updatedOrder.Metadata["cancellationReason"].ToString().Should().Be(cancellationReason);
    }

    [Fact]
    public async Task DeleteAsync_ExistingOrder_ShouldRemoveFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _unitOfWork.OrderWriter.DeleteAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var result = await _unitOfWork.OrderReader.GetByIdAsync(order.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingOrder_ShouldReturnOrder()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.UserId.Should().Be(user.Id);
        result.Status.Should().Be(order.Status);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentOrder_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.OrderWriter.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUserOrders_ShouldReturnUserOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var otherUser = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.UserWriter.AddAsync(otherUser);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var order2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var otherUserOrder = new OrderBuilder().WithUser(otherUser).WithShippingAddress(shippingAddress).Build();

        await _unitOfWork.OrderWriter.AddAsync(order1);
        await _unitOfWork.OrderWriter.AddAsync(order2);
        await _unitOfWork.OrderWriter.AddAsync(otherUserOrder);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderWriter.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(new[] { order1.Id, order2.Id });
        result.Should().OnlyContain(o => o.UserId == user.Id);
    }

    [Fact]
    public async Task GetByStatusAsync_ExistingOrdersWithStatus_ShouldReturnMatchingOrders()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var pendingOrder1 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var pendingOrder2 = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).Build();
        var shippedOrder = new OrderBuilder().WithUser(user).WithShippingAddress(shippingAddress).AsShippedOrder().Build();

        await _unitOfWork.OrderWriter.AddAsync(pendingOrder1);
        await _unitOfWork.OrderWriter.AddAsync(pendingOrder2);
        await _unitOfWork.OrderWriter.AddAsync(shippedOrder);
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _unitOfWork.OrderWriter.GetByStatusAsync(OrderStatus.Pending);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().Contain(new[] { pendingOrder1.Id, pendingOrder2.Id });
        result.Should().OnlyContain(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentUpdate_ShouldThrowConcurrencyException()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Act - Simulate concurrent access with two service scopes
        using var scope1 = ServiceProvider.CreateScope();
        using var scope2 = ServiceProvider.CreateScope();

        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var unitOfWork2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Both contexts load the same entity (same Version initially)
        var order1 = await unitOfWork1.OrderWriter.GetByIdAsync(order.Id);
        var order2 = await unitOfWork2.OrderWriter.GetByIdAsync(order.Id);

        // Both try to modify the order
        order1!.UpdateMetadata("concurrentTest", "first");
        order2!.UpdateMetadata("concurrentTest", "second");

        // First update should succeed (Version incremented)
        await unitOfWork1.OrderWriter.UpdateAsync(order1);
        await unitOfWork1.SaveChangesAsync();

        // Second update should fail with concurrency exception (stale Version)
        await unitOfWork2.OrderWriter.UpdateAsync(order2);
        var action = () => unitOfWork2.SaveChangesAsync();

        // Assert
        await action.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task UpdateAsync_ProcessFullRefund_ShouldUpdateRefundFields()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .AsPaidOrder()
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and process refund
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        var refundReason = "Customer not satisfied";
        existingOrder!.ProcessRefund(refundReason);

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Cancelled);
        updatedOrder.RefundedAmount.Should().Be(updatedOrder.Total);
        updatedOrder.RefundReason.Should().Be(refundReason);
        updatedOrder.Metadata.Should().ContainKey("refunded");
        updatedOrder.Metadata["refunded"].ToString().Should().Be("True");
    }

    [Fact]
    public async Task UpdateAsync_ProcessPartialRefund_ShouldUpdateRefundedAmount()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .WithSubtotal(100.00m)
            .WithTax(10.00m)
            .WithShippingCost(5.00m)
            .AsPaidOrder()
            .Build();

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Detach to simulate real-world scenario
        DbContext.Entry(order).State = EntityState.Detached;

        // Act - Load fresh entity and process partial refund
        var existingOrder = await _unitOfWork.OrderWriter.GetByIdAsync(order.Id);
        var partialRefundAmount = Money.Create(50.00m, "USD").Value;
        var refundReason = "Partial refund for damaged item";
        existingOrder!.ProcessPartialRefund(partialRefundAmount, refundReason);

        await _unitOfWork.OrderWriter.UpdateAsync(existingOrder);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        var updatedOrder = await _unitOfWork.OrderReader.GetDetailByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Processing); // Should still be processing, not cancelled
        updatedOrder.RefundedAmount.Should().Be(50.00m);
        updatedOrder.Metadata.Should().ContainKey("partialRefunds");
    }

    [Fact]
    public async Task GetOrderItemAsync_ExistingOrderItem_ShouldReturnOrderItem()
    {
        // Arrange
        await ResetDatabaseAsync();

        var user = new UserBuilder().Build();
        var shippingAddress = new AddressBuilder().WithUser(user).Build();
        await _unitOfWork.UserWriter.AddAsync(user);
        await _unitOfWork.AddressWriter.AddAsync(shippingAddress);

        var category = new CategoryBuilder().Build();
        await _categoryWriteRepository.AddAsync(category);

        var product = new ProductBuilder().WithCategory(category).Build();
        await _unitOfWork.ProductWriter.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var order = new OrderBuilder()
            .WithUser(user)
            .WithShippingAddress(shippingAddress)
            .Build();

        var unitPrice = Money.Create(29.99m, "USD").Value;
        order.AddItem(product, 2, unitPrice);

        await _unitOfWork.OrderWriter.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        var orderItem = order.Items.First();

        // Act
        var result = await _unitOfWork.OrderWriter.GetOrderItemAsync(orderItem.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(orderItem.Id);
        result.ProductId.Should().Be(product.Id);
        result.Quantity.Should().Be(2);
        result.UnitPrice.Amount.Should().Be(29.99m);
        result.TotalPrice.Amount.Should().Be(59.98m);
    }

    [Fact]
    public async Task GetOrderItemAsync_NonExistentOrderItem_ShouldReturnNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentOrderItemId = Guid.NewGuid();

        // Act
        var result = await _unitOfWork.OrderWriter.GetOrderItemAsync(nonExistentOrderItemId);

        // Assert
        result.Should().BeNull();
    }
}
