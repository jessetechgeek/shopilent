using FluentAssertions;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;

namespace Shopilent.Domain.Tests.Sales;

public class OrderItemTests
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
            user.Id,
            postalAddressResult.Value);

        addressResult.IsSuccess.Should().BeTrue();
        return addressResult.Value;
    }

    private Order CreateTestOrder(User user, Address address)
    {
        var subtotalResult = Money.Create(0, "USD");
        subtotalResult.IsSuccess.Should().BeTrue();

        var taxResult = Money.Create(10, "USD");
        taxResult.IsSuccess.Should().BeTrue();

        var shippingCostResult = Money.Create(5, "USD");
        shippingCostResult.IsSuccess.Should().BeTrue();

        var orderResult = Order.Create(
            user.Id,
            address.Id,
            address.Id,
            subtotalResult.Value,
            taxResult.Value,
            shippingCostResult.Value);

        orderResult.IsSuccess.Should().BeTrue();
        return orderResult.Value;
    }

    private Product CreateTestProduct(string name = "Test Product", decimal price = 50)
    {
        var slugResult = Slug.Create(name.ToLower().Replace(" ", "-"));
        slugResult.IsSuccess.Should().BeTrue();
        var slug = slugResult.Value;

        var priceResult = Money.Create(price, "USD");
        priceResult.IsSuccess.Should().BeTrue();
        var basePrice = priceResult.Value;

        var productResult = Product.Create(name, slug, basePrice, "TEST-SKU");
        productResult.IsSuccess.Should().BeTrue();
        return productResult.Value;
    }

    private ProductVariant CreateTestVariant(Product product, string sku = "VAR-SKU", decimal price = 60)
    {
        var priceResult = Money.Create(price, "USD");
        priceResult.IsSuccess.Should().BeTrue();

        var variantResult = ProductVariant.Create(product.Id, sku, priceResult.Value, 10);
        variantResult.IsSuccess.Should().BeTrue();
        return variantResult.Value;
    }

    [Fact]
    public void Create_ShouldCaptureProductDataSnapshot()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var quantity = 2;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice);

        // Assert
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        // Verify product data snapshot
        orderItem.ProductData.Should().NotBeNull();
        orderItem.ProductData.Should().ContainKey("name");
        orderItem.ProductData["name"].Should().Be(product.Name);
        orderItem.ProductData.Should().ContainKey("sku");
        orderItem.ProductData["sku"].Should().Be(product.Sku);
        orderItem.ProductData.Should().ContainKey("slug");
        orderItem.ProductData["slug"].Should().Be(product.Slug.Value);
    }

    [Fact]
    public void Create_WithVariant_ShouldCaptureVariantDataInSnapshot()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var variant = CreateTestVariant(product);
        var quantity = 1;

        var unitPriceResult = Money.Create(60, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice, variant);

        // Assert
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        // Verify variant data in snapshot
        orderItem.ProductData.Should().ContainKey("variant_sku");
        orderItem.ProductData["variant_sku"].Should().Be(variant.Sku);
        orderItem.ProductData.Should().ContainKey("variant_attributes");
    }

    [Fact]
    public void Create_ShouldCalculateTotalPriceCorrectly()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var quantity = 3;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        // Act
        var orderItemResult = order.AddItem(product, quantity, unitPrice);

        // Assert
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;
        orderItem.TotalPrice.Amount.Should().Be(unitPrice.Amount * quantity);
        orderItem.TotalPrice.Currency.Should().Be(unitPrice.Currency);
    }

    [Fact]
    public void UpdateQuantity_WithPositiveValue_ShouldUpdateQuantityAndTotalPrice()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var initialQuantity = 2;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        var orderItemResult = order.AddItem(product, initialQuantity, unitPrice);
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        var initialTotalPrice = orderItem.TotalPrice.Amount;
        initialTotalPrice.Should().Be(unitPrice.Amount * initialQuantity);

        var newQuantity = 5;

        // Act
        var updateResult = order.UpdateOrderItemQuantity(orderItem.Id, newQuantity);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        orderItem.Quantity.Should().Be(newQuantity);
        orderItem.TotalPrice.Amount.Should().Be(unitPrice.Amount * newQuantity);
    }

    [Fact]
    public void UpdateQuantity_WithZeroValue_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var initialQuantity = 2;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        var orderItemResult = order.AddItem(product, initialQuantity, unitPrice);
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        // Act
        var zeroResult = order.UpdateOrderItemQuantity(orderItem.Id, 0);

        // Assert
        zeroResult.IsFailure.Should().BeTrue();
        zeroResult.Error.Code.Should().Be("Order.InvalidQuantity");
        orderItem.Quantity.Should().Be(initialQuantity); // Quantity should remain unchanged
    }

    [Fact]
    public void UpdateQuantity_WithNegativeValue_ShouldReturnFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);
        var product = CreateTestProduct();
        var initialQuantity = 2;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        var orderItemResult = order.AddItem(product, initialQuantity, unitPrice);
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        // Act
        var negativeResult = order.UpdateOrderItemQuantity(orderItem.Id, -1);

        // Assert
        negativeResult.IsFailure.Should().BeTrue();
        negativeResult.Error.Code.Should().Be("Order.InvalidQuantity");
        orderItem.Quantity.Should().Be(initialQuantity); // Quantity should remain unchanged
    }

    [Fact]
    public void ProductDataSnapshot_ShouldBeUnaffectedByProductChanges()
    {
        // Arrange
        var user = CreateTestUser();
        var address = CreateTestAddress(user);
        var order = CreateTestOrder(user, address);

        var productName = "Original Product";
        var product = CreateTestProduct(productName);
        var quantity = 1;

        var unitPriceResult = Money.Create(50, "USD");
        unitPriceResult.IsSuccess.Should().BeTrue();
        var unitPrice = unitPriceResult.Value;

        var orderItemResult = order.AddItem(product, quantity, unitPrice);
        orderItemResult.IsSuccess.Should().BeTrue();
        var orderItem = orderItemResult.Value;

        // Verify original product name is in snapshot
        orderItem.ProductData["name"].Should().Be(productName);

        // Now change the product
        var newSlugResult = Slug.Create("updated-product");
        newSlugResult.IsSuccess.Should().BeTrue();

        var newPriceResult = Money.FromDollars(70);
        newPriceResult.IsSuccess.Should().BeTrue();

        product.Update(
            "Updated Product Name",
            newSlugResult.Value,
            newPriceResult.Value,
            "New description",
            "NEW-SKU");

        // Act & Assert - Snapshot should remain unchanged
        orderItem.ProductData["name"].Should().Be(productName);
        orderItem.ProductData["name"].Should().NotBe("Updated Product Name");
        orderItem.ProductData["sku"].Should().Be("TEST-SKU");
        orderItem.ProductData["sku"].Should().NotBe("NEW-SKU");
    }
}
