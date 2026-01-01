using FluentAssertions;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Enums;
using Shopilent.Domain.Sales.Events;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Domain.Payments.Enums;

namespace Shopilent.Domain.Tests.Sales;

public class OrderTests
{
    private User CreateTestUser()
    {
        var emailResult = Email.Create("test@example.com");
        emailResult.IsSuccess.Should().BeTrue();

        var fullNameResult = FullName.Create("Test", "User");
        fullNameResult.IsSuccess.Should().BeTrue();

        var userResult = User.Create(
            emailResult.Value,
            "hashed_password",
            fullNameResult.Value);

        userResult.IsSuccess.Should().BeTrue();
        return userResult.Value;
    }

    private Address CreateTestAddress(User user)
    {
        var postalAddressResult = PostalAddress.Create(
            "123 Main St",
            "Anytown",
            "State",
            "Country",
            "12345");
        postalAddressResult.IsSuccess.Should().BeTrue();

        var addressResult = Address.CreateShipping(
            user,
            postalAddressResult.Value);

        addressResult.IsSuccess.Should().BeTrue();
        return addressResult.Value;
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateOrder()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        var shippingMethod = "Standard";

        // Act
        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost,
            shippingMethod);

        // Assert
        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.UserId.Should().Be(user.Id);
        order.ShippingAddressId.Should().Be(shippingAddress.Id);
        order.BillingAddressId.Should().Be(billingAddress.Id);
        order.Subtotal.Should().Be(subtotal);
        order.Tax.Should().Be(tax);
        order.ShippingCost.Should().Be(shippingCost);
        order.ShippingMethod.Should().Be(shippingMethod);
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        // Total should be sum of subtotal, tax, and shipping
        var expectedTotalResult = Money.Create(115, "USD");
        expectedTotalResult.IsSuccess.Should().BeTrue();
        var expectedTotal = expectedTotalResult.Value;
        order.Total.Amount.Should().Be(expectedTotal.Amount);

        order.Items.Should().BeEmpty();
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedEvent);
    }

    [Fact]
    public void Create_WithNullSubtotal_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);
        Money subtotal = null;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        // Act
        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        // Assert
        orderResult.IsFailure.Should().BeTrue();
        orderResult.Error.Code.Should().Be("Payment.NegativeAmount");
    }

    [Fact]
    public void Create_WithNullTax_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        Money tax = null;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        // Act
        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        // Assert
        orderResult.IsFailure.Should().BeTrue();
        orderResult.Error.Code.Should().Be("Payment.NegativeAmount");
    }

    [Fact]
    public void Create_WithNullShippingCost_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        Money shippingCost = null;

        // Act
        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        // Assert
        orderResult.IsFailure.Should().BeTrue();
        orderResult.Error.Code.Should().Be("Payment.NegativeAmount");
    }

    [Fact]
    public void CreatePaidOrder_ShouldCreateOrderWithPaidStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        // Act
        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        // Assert
        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        order.Status.Should().Be(OrderStatus.Processing);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedEvent);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidEvent);
    }

    // Removing CreateFromCart test as this method doesn't exist anymore

    [Fact]
    public void AddItem_ShouldAddItemToOrder()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(0, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();

        var productPriceResult = Money.Create(50, "USD");
        productPriceResult.IsSuccess.Should().BeTrue();

        var productResult = Product.Create(
            "Test Product",
            slugResult.Value,
            productPriceResult.Value);

        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var quantity = 2;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice);

        // Assert
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;
        order.Items.Should().HaveCount(1);
        orderItem.ProductId.Should().Be(product.Id);
        orderItem.Quantity.Should().Be(quantity);
        orderItem.UnitPrice.Should().Be(unitPrice);

        // Total price for the item should be unit price * quantity
        var expectedItemTotalResult = Money.Create(100, "USD");
        expectedItemTotalResult.IsSuccess.Should().BeTrue();
        var expectedItemTotal = expectedItemTotalResult.Value;
        orderItem.TotalPrice.Amount.Should().Be(expectedItemTotal.Amount);

        // Order subtotal should be updated
        order.Subtotal.Amount.Should().Be(expectedItemTotal.Amount);

        // Order total should be updated (subtotal + tax + shipping)
        var expectedOrderTotalResult = Money.Create(115, "USD");
        expectedOrderTotalResult.IsSuccess.Should().BeTrue();
        var expectedOrderTotal = expectedOrderTotalResult.Value;
        order.Total.Amount.Should().Be(expectedOrderTotal.Amount);
    }

    [Fact]
    public void AddItem_WithNullProduct_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(0, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(0, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(0, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        Product product = null;
        var quantity = 1;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice);

        // Assert
        orderItemResult.IsFailure.Should().BeTrue();
        orderItemResult.Error.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var moneyResult = Money.Create(0, "USD");
        moneyResult.IsSuccess.Should().BeTrue();
        var money = moneyResult.Value;

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            money,
            money,
            money);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();

        var priceResult = Money.Create(50, "USD");
        priceResult.IsSuccess.Should().BeTrue();

        var productResult = Product.Create(
            "Test Product",
            slugResult.Value,
            priceResult.Value);

        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var quantity = 0;

        // Act
        var orderItemResult = order.AddItem(product, quantity, priceResult.Value);

        // Assert
        orderItemResult.IsFailure.Should().BeTrue();
        orderItemResult.Error.Code.Should().Be("Order.InvalidQuantity");
    }

    [Fact]
    public void AddItem_WithNonPendingOrder_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        // Change order status from Pending
        var paidResult = order.MarkAsPaid();
        paidResult.IsSuccess.Should().BeTrue();

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        order.Status.Should().Be(OrderStatus.Shipped);

        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();

        var priceResult = Money.Create(50, "USD");
        priceResult.IsSuccess.Should().BeTrue();

        var productResult = Product.Create(
            "Test Product",
            slugResult.Value,
            priceResult.Value);

        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var quantity = 1;

        // Act
        var orderItemResult = order.AddItem(product, quantity, priceResult.Value);

        // Assert
        orderItemResult.IsFailure.Should().BeTrue();
        orderItemResult.Error.Code.Should().Be("Order.InvalidStatus");
    }

    [Fact]
    public void AddItem_WithProductVariant_ShouldAddItemWithVariant()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(0, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var slugResult = Slug.Create("test-product");
        slugResult.IsSuccess.Should().BeTrue();

        var basePriceResult = Money.Create(50, "USD");
        basePriceResult.IsSuccess.Should().BeTrue();

        var productResult = Product.Create(
            "Test Product",
            slugResult.Value,
            basePriceResult.Value);

        productResult.IsSuccess.Should().BeTrue();
        var product = productResult.Value;

        var variantPriceResult = Money.Create(60, "USD");
        variantPriceResult.IsSuccess.Should().BeTrue();

        var variantResult = ProductVariant.Create(
            product.Id,
            "VAR-123",
            variantPriceResult.Value,
            10);

        variantResult.IsSuccess.Should().BeTrue();
        var variant = variantResult.Value;

        var quantity = 1;
        var unitPrice = variantPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice, variant);

        // Assert
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;
        order.Items.Should().HaveCount(1);
        orderItem.ProductId.Should().Be(product.Id);
        orderItem.VariantId.Should().Be(variant.Id);
        orderItem.Quantity.Should().Be(quantity);
        orderItem.UnitPrice.Should().Be(unitPrice);

        // Product data should include variant info
        orderItem.ProductData.Should().ContainKey("variant_sku");
        orderItem.ProductData["variant_sku"].Should().Be(variant.Sku);
    }

    [Fact]
    public void UpdateOrderStatus_ShouldChangeStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.Status.Should().Be(OrderStatus.Pending);

        // Act
        var updateResult = order.UpdateOrderStatus(OrderStatus.Processing);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Processing);
        order.DomainEvents.Should().ContainSingle(e => e is OrderStatusChangedEvent);
    }

    [Fact]
    public void UpdatePaymentStatus_ShouldChangePaymentStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        // Act
        var updateResult = order.UpdatePaymentStatus(PaymentStatus.Succeeded);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        order.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaymentStatusChangedEvent);
    }

    [Fact]
    public void MarkAsPaid_ShouldUpdateStatusesCorrectly()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.Status.Should().Be(OrderStatus.Pending);

        // Act
        var paidResult = order.MarkAsPaid();

        // Assert
        paidResult.IsSuccess.Should().BeTrue();
        order.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        order.Status.Should().Be(OrderStatus.Processing);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidEvent);
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_ShouldNotChangeStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.PaymentStatus.Should().Be(PaymentStatus.Succeeded);

        // Clear domain events for fresh test
        order.ClearDomainEvents();

        // Act
        var paidResult = order.MarkAsPaid();

        // Assert
        paidResult.IsSuccess.Should().BeTrue();
        order.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        order.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void MarkAsShipped_ShouldUpdateStatusCorrectly()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.Status.Should().Be(OrderStatus.Processing);
        var trackingNumber = "TRACK123456";

        // Act
        var shippedResult = order.MarkAsShipped(trackingNumber);

        // Assert
        shippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
        order.Metadata["trackingNumber"].Should().Be(trackingNumber);
        order.DomainEvents.Should().ContainSingle(e => e is OrderShippedEvent);
    }

    [Fact]
    public void MarkAsShipped_WithUnpaidOrder_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        // Act
        var shippedResult = order.MarkAsShipped();

        // Assert
        shippedResult.IsFailure.Should().BeTrue();
        shippedResult.Error.Code.Should().Be("Order.PaymentRequired");
    }

    [Fact]
    public void MarkAsShipped_WhenAlreadyShipped_ShouldNotChangeStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped("TRACK123");
        shippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);

        // Clear domain events for fresh test
        order.ClearDomainEvents();

        // Act
        var secondShippedResult = order.MarkAsShipped("TRACK456");

        // Assert
        secondShippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
        order.Metadata["trackingNumber"].Should().Be("TRACK123"); // Shouldn't change
        order.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void MarkAsDelivered_ShouldUpdateStatusCorrectly()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);

        // Act
        var deliveredResult = order.MarkAsDelivered();

        // Assert
        deliveredResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DomainEvents.Should().ContainSingle(e => e is OrderDeliveredEvent);
    }

    [Fact]
    public void MarkAsDelivered_WithoutShipping_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        order.Status.Should().Be(OrderStatus.Processing);

        // Act
        var deliveredResult = order.MarkAsDelivered();

        // Assert
        deliveredResult.IsFailure.Should().BeTrue();
        deliveredResult.Error.Code.Should().Be("Order.InvalidStatus");
    }

    [Fact]
    public void MarkAsDelivered_WhenAlreadyDelivered_ShouldNotChangeStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        var deliveredResult = order.MarkAsDelivered();
        deliveredResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);

        // Clear domain events for fresh test
        order.ClearDomainEvents();

        // Act
        var secondDeliveredResult = order.MarkAsDelivered();

        // Assert
        secondDeliveredResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void Cancel_ShouldCancelOrder()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var reason = "Customer request";

        // Act - default behavior (customer)
        var cancelResult = order.Cancel(reason);

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Metadata["cancellationReason"].Should().Be(reason);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldCancelWithoutReason()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        // Act - default behavior (customer)
        var cancelResult = order.Cancel();

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Metadata.Should().NotContainKey("cancellationReason");
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_DeliveredOrder_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        var deliveredResult = order.MarkAsDelivered();
        deliveredResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);

        // Act - default behavior (customer)
        var cancelResult = order.Cancel();

        // Assert
        cancelResult.IsFailure.Should().BeTrue();
        cancelResult.Error.Code.Should().Be("Order.InvalidStatus");
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ShouldNotRaiseEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var cancelResult = order.Cancel("Initial reason");
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);

        // Clear domain events for fresh test
        order.ClearDomainEvents();

        // Act
        var secondCancelResult = order.Cancel("New reason");

        // Assert
        secondCancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Metadata["cancellationReason"].Should().Be("Initial reason"); // Shouldn't change
        order.DomainEvents.Should().BeEmpty(); // No events should be raised
    }

    [Fact]
    public void UpdateMetadata_ShouldAddOrUpdateMetadata()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var key = "note";
        var value = "Please leave at the door";

        // Act
        var updateResult = order.UpdateMetadata(key, value);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        order.Metadata.Should().ContainKey(key);
        order.Metadata[key].Should().Be(value);
    }

    [Fact]
    public void UpdateMetadata_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var key = string.Empty;
        var value = "test value";

        // Act
        var updateResult = order.UpdateMetadata(key, value);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be("Order.InvalidMetadataKey");
    }

    [Fact]
    public void AddMultipleItems_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(0, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();
        var subtotal = subtotalResult.Value;

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();
        var tax = taxResult.Value;

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();
        var shippingCost = shippingCostResult.Value;

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotal,
            tax,
            shippingCost);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var slugResult1 = Slug.Create("product-1");
        slugResult1.IsSuccess.Should().BeTrue();

        var priceResult1 = Money.FromDollars(50);
        priceResult1.IsSuccess.Should().BeTrue();

        var productResult1 = Product.Create("Product 1", slugResult1.Value, priceResult1.Value);
        productResult1.IsSuccess.Should().BeTrue();
        var product1 = productResult1.Value;

        var slugResult2 = Slug.Create("product-2");
        slugResult2.IsSuccess.Should().BeTrue();

        var priceResult2 = Money.FromDollars(75);
        priceResult2.IsSuccess.Should().BeTrue();

        var productResult2 = Product.Create("Product 2", slugResult2.Value, priceResult2.Value);
        productResult2.IsSuccess.Should().BeTrue();
        var product2 = productResult2.Value;

        var slugResult3 = Slug.Create("product-3");
        slugResult3.IsSuccess.Should().BeTrue();

        var priceResult3 = Money.FromDollars(25);
        priceResult3.IsSuccess.Should().BeTrue();

        var productResult3 = Product.Create("Product 3", slugResult3.Value, priceResult3.Value);
        productResult3.IsSuccess.Should().BeTrue();
        var product3 = productResult3.Value;

        // Act
        order.AddItem(product1, 2, priceResult1.Value); // $100
        order.AddItem(product2, 1, priceResult2.Value); // $75
        order.AddItem(product3, 3, priceResult3.Value); // $75

        // Assert
        order.Items.Count.Should().Be(3);

        // Expected subtotal: $100 + $75 + $75 = $250
        order.Subtotal.Amount.Should().Be(250m);

        // Expected total: $250 + $10 + $5 = $265
        order.Total.Amount.Should().Be(265m);
    }

    [Fact]
    public void Cancel_CustomerCancellPendingOrder_ShouldSucceed()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.Status.Should().Be(OrderStatus.Pending);

        // Act
        var cancelResult = order.Cancel("Customer request", isAdminOrManager: false);

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_CustomerCancelProcessingOrder_ShouldSucceed()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        order.Status.Should().Be(OrderStatus.Processing);

        // Act
        var cancelResult = order.Cancel("Customer request", isAdminOrManager: false);

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_CustomerCancelShippedOrder_ShouldFail()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);

        // Act
        var cancelResult = order.Cancel("Customer request", isAdminOrManager: false);

        // Assert
        cancelResult.IsFailure.Should().BeTrue();
        cancelResult.Error.Code.Should().Be("Order.InvalidStatus");
        cancelResult.Error.Message.Should().Contain("cancel - only pending or processing orders can be cancelled by customers");
    }

    [Fact]
    public void Cancel_AdminCancelShippedOrder_ShouldSucceed()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);

        // Act
        var cancelResult = order.Cancel("Admin cancellation", isAdminOrManager: true);

        // Assert
        cancelResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_AdminCancelDeliveredOrder_ShouldFail()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        var shippedResult = order.MarkAsShipped();
        shippedResult.IsSuccess.Should().BeTrue();

        var deliveredResult = order.MarkAsDelivered();
        deliveredResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);

        // Act
        var cancelResult = order.Cancel("Admin cancellation", isAdminOrManager: true);

        // Assert
        cancelResult.IsFailure.Should().BeTrue();
        cancelResult.Error.Code.Should().Be("Order.InvalidStatus");
        cancelResult.Error.Message.Should().Contain("cancel - delivered orders cannot be cancelled");
    }

    [Fact]
    public void ProcessRefund_WhenOrderIsReturned_ShouldSetStatusToReturnedAndRefunded()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        // Ship and deliver the order
        order.MarkAsShipped();
        order.MarkAsDelivered();

        // Mark as returned
        var returnResult = order.MarkAsReturned("Customer not satisfied");
        returnResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Returned);

        // Act - Process refund for returned order
        var refundResult = order.ProcessRefund("Refund for returned item");

        // Assert
        refundResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.ReturnedAndRefunded);
        order.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        order.RefundedAmount.Should().Be(order.Total);
        order.RefundedAt.Should().NotBeNull();
        order.DomainEvents.Should().Contain(e => e is OrderRefundedEvent);
    }

    [Fact]
    public void ProcessRefund_WhenOrderIsNotReturned_ShouldSetStatusToCancelled()
    {
        // Arrange
        var user = CreateTestUser();
        var shippingAddress = CreateTestAddress(user);
        var billingAddress = CreateTestAddress(user);

        var subtotalResult = Money.Create(100, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.CreatePaidOrder(
            user,
            shippingAddress,
            billingAddress,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;

        // Order is in Processing status (not returned)
        order.Status.Should().Be(OrderStatus.Processing);

        // Act - Process refund for non-returned order
        var refundResult = order.ProcessRefund("Admin discretion refund");

        // Assert
        refundResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        order.RefundedAmount.Should().Be(order.Total);
        order.RefundedAt.Should().NotBeNull();
        order.DomainEvents.Should().Contain(e => e is OrderRefundedEvent);
    }
}