using Bogus;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.ValueObjects;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Enums;
using Shopilent.Domain.Identity.ValueObjects;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.ValueObjects;
using Shopilent.Domain.Shipping;
using Shopilent.Domain.Shipping.ValueObjects;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Attribute = Shopilent.Domain.Catalog.Attribute;

namespace Shopilent.API.IntegrationTests.Common;

/// <summary>
/// Centralized database seeding utilities for all integration tests.
/// Provides direct database access for efficient test data creation across all domains.
/// </summary>
public static class TestDbSeeder
{
    private static readonly Faker _faker = new();

    #region Catalog Domain Seeders

    /// <summary>
    /// Seeds a product with the specified parameters or generates random values.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="name">Product name (auto-generated if null)</param>
    /// <param name="slug">Product slug (auto-generated if null)</param>
    /// <param name="basePrice">Product base price (defaults to 99.99 USD if null)</param>
    /// <param name="description">Product description (auto-generated if null)</param>
    /// <param name="sku">Product SKU (auto-generated if null)</param>
    /// <param name="category">Optional category entity to associate with product</param>
    /// <returns>Created and persisted Product entity</returns>
    public static async Task<Product> SeedProductAsync(
        Func<Func<ApplicationDbContext, Task<Product>>, Task<Product>> executeDbContext,
        string? name = null,
        Slug? slug = null,
        Money? basePrice = null,
        string? description = null,
        string? sku = null,
        Category? category = null)
    {
        return await executeDbContext(async context =>
        {
            var productName = name ?? $"Test Product {Guid.NewGuid():N}";
            var productSlug = slug ?? Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = basePrice ?? Money.Create(99.99m, "USD").Value;
            var productDescription = description ?? _faker.Commerce.ProductDescription();
            var productSku = sku ?? $"SKU-{Guid.NewGuid():N}";

            var product = Product.CreateWithDescription(
                name: productName,
                slug: productSlug,
                basePrice: productPrice,
                description: productDescription,
                sku: productSku
            ).Value;

            // Associate with category if provided
            if (category != null)
            {
                product.AddCategory(category);
            }

            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product;
        });
    }

    /// <summary>
    /// Seeds a product variant for an existing product.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="productId">Product ID to create variant for</param>
    /// <param name="sku">Variant SKU (auto-generated if null)</param>
    /// <param name="price">Variant price (defaults to 89.99 USD if null)</param>
    /// <param name="stockQuantity">Initial stock quantity (defaults to 100)</param>
    /// <returns>Created and persisted ProductVariant entity</returns>
    public static async Task<ProductVariant> SeedProductVariantAsync(
        Func<Func<ApplicationDbContext, Task<ProductVariant>>, Task<ProductVariant>> executeDbContext,
        Guid productId,
        string? sku = null,
        Money? price = null,
        int stockQuantity = 100)
    {
        return await executeDbContext(async context =>
        {
            var product = await context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {productId} not found.");
            }

            var variantSku = sku ?? $"VAR-{Guid.NewGuid():N}";
            var variantPrice = price ?? Money.Create(89.99m, "USD").Value;

            var variantResult = ProductVariant.Create(
                productId: productId,
                sku: variantSku,
                price: variantPrice,
                stockQuantity: stockQuantity
            );

            if (variantResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to create variant: {variantResult.Error}");
            }

            var variant = variantResult.Value;
            var addResult = product.AddVariant(variant);

            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add variant to product: {addResult.Error}");
            }

            await context.SaveChangesAsync();
            return variant;
        });
    }

    /// <summary>
    /// Seeds a category with the specified parameters or generates random values.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="name">Category name (auto-generated if null)</param>
    /// <param name="slug">Category slug (auto-generated if null)</param>
    /// <param name="parent">Optional parent category entity for hierarchical structure</param>
    /// <param name="isActive">Category active status (defaults to true)</param>
    /// <returns>Created and persisted Category entity</returns>
    public static async Task<Category> SeedCategoryAsync(
        Func<Func<ApplicationDbContext, Task<Category>>, Task<Category>> executeDbContext,
        string? name = null,
        Slug? slug = null,
        Category? parent = null,
        bool isActive = true)
    {
        return await executeDbContext(async context =>
        {
            var categoryName = name ?? $"Test Category {Guid.NewGuid():N}";
            var categorySlug = slug ?? Slug.Create($"test-category-{Guid.NewGuid():N}").Value;

            var category = Category.Create(
                name: categoryName,
                slug: categorySlug,
                parent: parent
            ).Value;

            if (!isActive)
            {
                category.Deactivate();
            }

            context.Categories.Add(category);
            await context.SaveChangesAsync();
            return category;
        });
    }

    /// <summary>
    /// Seeds an attribute with the specified parameters or generates random values.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="name">Attribute name (auto-generated if null)</param>
    /// <param name="displayName">Attribute display name (auto-generated if null)</param>
    /// <param name="type">Attribute type (defaults to Text)</param>
    /// <returns>Created and persisted Attribute entity</returns>
    public static async Task<Attribute> SeedAttributeAsync(
        Func<Func<ApplicationDbContext, Task<Attribute>>, Task<Attribute>> executeDbContext,
        string? name = null,
        string? displayName = null,
        AttributeType type = AttributeType.Text)
    {
        return await executeDbContext(async context =>
        {
            var attributeName = name ?? $"test-attribute-{Guid.NewGuid():N}";
            var attributeDisplayName = displayName ?? $"Test Attribute {Guid.NewGuid():N}";

            var attribute = Attribute.Create(
                name: attributeName,
                displayName: attributeDisplayName,
                type: type
            ).Value;

            context.Attributes.Add(attribute);
            await context.SaveChangesAsync();
            return attribute;
        });
    }

    #endregion

    #region Sales Domain Seeders

    /// <summary>
    /// Seeds an empty cart for a specific user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="user">User entity who owns the cart</param>
    /// <returns>Created and persisted Cart entity</returns>
    public static async Task<Cart> SeedCartAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        User user)
    {
        return await executeDbContext(async context =>
        {
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds an anonymous cart (no user association).
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <returns>Created and persisted Cart entity</returns>
    public static async Task<Cart> SeedAnonymousCartAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext)
    {
        return await executeDbContext(async context =>
        {
            var cart = Cart.Create().Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds a cart with pre-populated items for testing.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="user">User entity who owns the cart (null for anonymous)</param>
    /// <param name="itemCount">Number of items to add to cart (defaults to 3)</param>
    /// <returns>Created and persisted Cart entity with items</returns>
    public static async Task<Cart> SeedCartWithItemsAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        User? user = null,
        int itemCount = 3)
    {
        return await executeDbContext(async context =>
        {
            // Create cart
            var cart = Cart.Create(user?.Id).Value;

            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add items to cart
            for (int i = 0; i < itemCount; i++)
            {
                var productName = $"Cart Item Product {i + 1} {Guid.NewGuid():N}";
                var productSlug = Slug.Create($"cart-item-{i + 1}-{Guid.NewGuid():N}").Value;
                var productPrice = Money.Create(_faker.Random.Decimal(10, 200), "USD").Value;

                var product = Product.CreateWithDescription(
                    name: productName,
                    slug: productSlug,
                    basePrice: productPrice,
                    description: _faker.Commerce.ProductDescription(),
                    sku: $"SKU-{Guid.NewGuid():N}"
                ).Value;

                context.Products.Add(product);
                await context.SaveChangesAsync();

                var quantity = _faker.Random.Int(1, 5);
                cart.AddItem(product.Id, quantity);
            }

            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds a cart for a specific user ID.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="userId">User ID who owns the cart</param>
    /// <returns>Created and persisted Cart entity</returns>
    public static async Task<Cart> SeedCartForUserAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        Guid userId)
    {
        return await executeDbContext(async context =>
        {
            var user = await context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found.");
            }

            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds a cart item for an existing cart and product.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="cartId">Cart ID to add item to</param>
    /// <param name="productId">Product ID for the cart item</param>
    /// <param name="quantity">Quantity of the product (defaults to 1)</param>
    /// <returns>Created CartItem entity</returns>
    public static async Task<CartItem> SeedCartItemAsync(
        Func<Func<ApplicationDbContext, Task<CartItem>>, Task<CartItem>> executeDbContext,
        Guid cartId,
        Guid productId,
        int quantity = 1)
    {
        return await executeDbContext(async context =>
        {
            var cart = await context.Carts.FindAsync(cartId);
            if (cart == null)
            {
                throw new InvalidOperationException($"Cart with ID {cartId} not found.");
            }

            var product = await context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {productId} not found.");
            }

            var addResult = cart.AddItem(product.Id, quantity);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            // Return the newly created cart item
            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == null);
            if (cartItem == null)
            {
                throw new InvalidOperationException("Cart item was not created successfully.");
            }

            return cartItem;
        });
    }

    /// <summary>
    /// Seeds a cart item with a product variant for an existing cart.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="cartId">Cart ID to add item to</param>
    /// <param name="productId">Product ID for the cart item</param>
    /// <param name="variantId">Product variant ID for the cart item</param>
    /// <param name="quantity">Quantity of the product variant (defaults to 1)</param>
    /// <returns>Created CartItem entity</returns>
    public static async Task<CartItem> SeedCartItemWithVariantAsync(
        Func<Func<ApplicationDbContext, Task<CartItem>>, Task<CartItem>> executeDbContext,
        Guid cartId,
        Guid productId,
        Guid variantId,
        int quantity = 1)
    {
        return await executeDbContext(async context =>
        {
            var cart = await context.Carts.FindAsync(cartId);
            if (cart == null)
            {
                throw new InvalidOperationException($"Cart with ID {cartId} not found.");
            }

            var product = await context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {productId} not found.");
            }

            var variant = await context.ProductVariants.FindAsync(variantId);
            if (variant == null)
            {
                throw new InvalidOperationException($"Product variant with ID {variantId} not found.");
            }

            var addResult = cart.AddItem(product.Id, quantity, variant?.Id);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item with variant to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            // Return the newly created cart item
            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (cartItem == null)
            {
                throw new InvalidOperationException("Cart item with variant was not created successfully.");
            }

            return cartItem;
        });
    }

    /// <summary>
    /// Seeds an anonymous cart with a single item.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="initialQuantity">Initial quantity for the cart item (defaults to 5)</param>
    /// <returns>Tuple containing Cart and CartItem entities</returns>
    public static async Task<(Cart cart, CartItem cartItem)> SeedAnonymousCartWithItemAsync(
        Func<Func<ApplicationDbContext, Task<(Cart, CartItem)>>, Task<(Cart, CartItem)>> executeDbContext,
        int initialQuantity = 5)
    {
        return await executeDbContext(async context =>
        {
            // Create product
            var productName = $"Test Product {Guid.NewGuid():N}";
            var productSlug = Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = Money.Create(99.99m, "USD").Value;

            var product = Product.CreateWithDescription(
                name: productName,
                slug: productSlug,
                basePrice: productPrice,
                description: _faker.Commerce.ProductDescription(),
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create anonymous cart
            var cart = Cart.Create(null).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add item to cart
            var addResult = cart.AddItem(product.Id, initialQuantity, null);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            var cartItem = cart.Items.First();
            return (cart, cartItem);
        });
    }

    /// <summary>
    /// Seeds an anonymous cart with a single item that has a variant.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="initialQuantity">Initial quantity for the cart item (defaults to 5)</param>
    /// <returns>Tuple containing Cart and CartItem entities</returns>
    public static async Task<(Cart cart, CartItem cartItem)> SeedAnonymousCartWithItemAndVariantAsync(
        Func<Func<ApplicationDbContext, Task<(Cart, CartItem)>>, Task<(Cart, CartItem)>> executeDbContext,
        int initialQuantity = 5)
    {
        return await executeDbContext(async context =>
        {
            // Create product
            var productName = $"Test Product {Guid.NewGuid():N}";
            var productSlug = Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = Money.Create(99.99m, "USD").Value;

            var product = Product.CreateWithDescription(
                name: productName,
                slug: productSlug,
                basePrice: productPrice,
                description: _faker.Commerce.ProductDescription(),
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create variant
            var variantSku = $"VAR-{Guid.NewGuid():N}";
            var variantPrice = Money.Create(89.99m, "USD").Value;

            var variantResult = ProductVariant.Create(
                productId: product.Id,
                sku: variantSku,
                price: variantPrice,
                stockQuantity: 100
            );

            if (variantResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to create variant: {variantResult.Error}");
            }

            var variant = variantResult.Value;
            product.AddVariant(variant);
            await context.SaveChangesAsync();

            // Create anonymous cart
            var cart = Cart.Create(null).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add item with variant to cart
            var addResult = cart.AddItem(product.Id, initialQuantity, variant?.Id);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            var cartItem = cart.Items.First();
            return (cart, cartItem);
        });
    }

    /// <summary>
    /// Seeds an anonymous cart with multiple items.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="itemCount">Number of items to add (defaults to 3)</param>
    /// <param name="initialQuantity">Initial quantity per item (defaults to 5)</param>
    /// <returns>Tuple containing Cart and list of CartItem entities</returns>
    public static async Task<(Cart cart, List<CartItem> cartItems)> SeedAnonymousCartWithMultipleItemsAsync(
        Func<Func<ApplicationDbContext, Task<(Cart, List<CartItem>)>>, Task<(Cart, List<CartItem>)>> executeDbContext,
        int itemCount = 3,
        int initialQuantity = 5)
    {
        return await executeDbContext(async context =>
        {
            // Create anonymous cart
            var cart = Cart.Create(null).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Create products and add them to cart
            var cartItems = new List<CartItem>();

            for (int i = 0; i < itemCount; i++)
            {
                var productName = $"Test Product {i + 1} {Guid.NewGuid():N}";
                var productSlug = Slug.Create($"test-product-{i + 1}-{Guid.NewGuid():N}").Value;
                var productPrice = Money.Create(_faker.Random.Decimal(10, 200), "USD").Value;

                var product = Product.CreateWithDescription(
                    name: productName,
                    slug: productSlug,
                    basePrice: productPrice,
                    description: _faker.Commerce.ProductDescription(),
                    sku: $"SKU-{Guid.NewGuid():N}"
                ).Value;

                context.Products.Add(product);
                await context.SaveChangesAsync();

                var addResult = cart.AddItem(product.Id, initialQuantity, null);
                if (addResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
                }
            }

            await context.SaveChangesAsync();
            cartItems.AddRange(cart.Items);

            return (cart, cartItems);
        });
    }

    /// <summary>
    /// Seeds a cart with a single item for the standard customer user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="initialQuantity">Initial quantity for the cart item (defaults to 5)</param>
    /// <returns>Tuple containing Cart and CartItem entities</returns>
    public static async Task<(Cart cart, CartItem cartItem)> SeedCartWithItemForCustomerAsync(
        Func<Func<ApplicationDbContext, Task<(Cart, CartItem)>>, Task<(Cart, CartItem)>> executeDbContext,
        int initialQuantity = 5)
    {
        return await executeDbContext(async context =>
        {
            // Get the standard customer user
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException(
                    "Customer user not found. Ensure AuthenticateAsCustomerAsync() is called before this method.");
            }

            // Create product
            var productName = $"Test Product {Guid.NewGuid():N}";
            var productSlug = Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = Money.Create(99.99m, "USD").Value;

            var product = Product.CreateWithDescription(
                name: productName,
                slug: productSlug,
                basePrice: productPrice,
                description: _faker.Commerce.ProductDescription(),
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create cart for customer
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add item to cart
            var addResult = cart.AddItem(product.Id, initialQuantity, null);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            var cartItem = cart.Items.First();
            return (cart, cartItem);
        });
    }

    /// <summary>
    /// Seeds a cart with a single item for the standard admin user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="initialQuantity">Initial quantity for the cart item (defaults to 5)</param>
    /// <returns>Tuple containing Cart and CartItem entities</returns>
    public static async Task<(Cart cart, CartItem cartItem)> SeedCartWithItemForAdminAsync(
        Func<Func<ApplicationDbContext, Task<(Cart, CartItem)>>, Task<(Cart, CartItem)>> executeDbContext,
        int initialQuantity = 5)
    {
        return await executeDbContext(async context =>
        {
            // Get the standard admin user
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "admin@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException(
                    "Admin user not found. Ensure AuthenticateAsAdminAsync() is called before this method.");
            }

            // Create product
            var productName = $"Test Product {Guid.NewGuid():N}";
            var productSlug = Slug.Create($"test-product-{Guid.NewGuid():N}").Value;
            var productPrice = Money.Create(99.99m, "USD").Value;

            var product = Product.CreateWithDescription(
                name: productName,
                slug: productSlug,
                basePrice: productPrice,
                description: _faker.Commerce.ProductDescription(),
                sku: $"SKU-{Guid.NewGuid():N}"
            ).Value;

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create cart for admin
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add item to cart
            var addResult = cart.AddItem(product.Id, initialQuantity, null);
            if (addResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
            }

            await context.SaveChangesAsync();

            var cartItem = cart.Items.First();
            return (cart, cartItem);
        });
    }

    /// <summary>
    /// Seeds an order with the specified parameters.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="user">User entity who placed the order</param>
    /// <param name="shippingAddress">Shipping address entity</param>
    /// <param name="billingAddress">Billing address entity (uses shipping address if null)</param>
    /// <param name="itemCount">Number of order items (defaults to 2)</param>
    /// <returns>Created and persisted Order entity</returns>
    public static async Task<Order> SeedOrderAsync(
        Func<Func<ApplicationDbContext, Task<Order>>, Task<Order>> executeDbContext,
        User user,
        Address shippingAddress,
        Address? billingAddress = null,
        int itemCount = 2)
    {
        return await executeDbContext(async context =>
        {
            // Create order items and calculate totals
            var subtotal = 0m;

            for (int i = 0; i < itemCount; i++)
            {
                var productName = $"Order Item Product {i + 1} {Guid.NewGuid():N}";
                var productSlug = Slug.Create($"order-item-{i + 1}-{Guid.NewGuid():N}").Value;
                var productPrice = _faker.Random.Decimal(10, 200);
                var quantity = _faker.Random.Int(1, 3);

                var product = Product.CreateWithDescription(
                    name: productName,
                    slug: productSlug,
                    basePrice: Money.Create(productPrice, "USD").Value,
                    description: _faker.Commerce.ProductDescription(),
                    sku: $"SKU-{Guid.NewGuid():N}"
                ).Value;

                context.Products.Add(product);
                await context.SaveChangesAsync();

                var itemTotal = productPrice * quantity;
                subtotal += itemTotal;
            }

            // Calculate tax and shipping
            var tax = Money.Create(subtotal * 0.08m, "USD").Value; // 8% tax
            var shippingCost = Money.Create(9.99m, "USD").Value;

            // Create order
            var order = Order.Create(
                user: user,
                shippingAddress: shippingAddress,
                billingAddress: billingAddress ?? shippingAddress,
                subtotal: Money.Create(subtotal, "USD").Value,
                tax: tax,
                shippingCost: shippingCost
            ).Value;

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order;
        });
    }

    #endregion

    #region Identity and Users Domain Seeders

    /// <summary>
    /// Seeds a user with the specified parameters or generates random values.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="email">User email (auto-generated if null)</param>
    /// <param name="firstName">User first name (auto-generated if null)</param>
    /// <param name="lastName">User last name (auto-generated if null)</param>
    /// <param name="passwordHash">Password hash (auto-generated if null)</param>
    /// <param name="role">User role (defaults to Customer)</param>
    /// <param name="isActive">User active status (defaults to true)</param>
    /// <param name="isEmailVerified">Email verification status (defaults to true)</param>
    /// <returns>Created and persisted User entity</returns>
    public static async Task<User> SeedUserAsync(
        Func<Func<ApplicationDbContext, Task<User>>, Task<User>> executeDbContext,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? passwordHash = null,
        UserRole role = UserRole.Customer,
        bool isActive = true,
        bool isEmailVerified = true)
    {
        return await executeDbContext(async context =>
        {
            var userEmail = Email.Create(email ?? _faker.Internet.Email()).Value;
            var userFirstName = firstName ?? _faker.Name.FirstName();
            var userLastName = lastName ?? _faker.Name.LastName();
            var fullName = FullName.Create(userFirstName, userLastName).Value;
            var userPasswordHash = passwordHash ?? $"HASH-{Guid.NewGuid():N}";

            var user = User.Create(
                email: userEmail,
                passwordHash: userPasswordHash,
                fullName: fullName,
                role: role
            ).Value;

            if (!isActive)
            {
                user.Deactivate();
            }

            if (isEmailVerified)
            {
                user.VerifyEmail();
            }

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        });
    }

    /// <summary>
    /// Gets the standard test customer user used by AuthenticateAsCustomerAsync.
    /// This user is created by the test framework with email "customer@shopilent.com".
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <returns>Customer User entity</returns>
    public static async Task<User> GetCustomerUserAsync(
        Func<Func<ApplicationDbContext, Task<User>>, Task<User>> executeDbContext)
    {
        return await executeDbContext(async context =>
        {
            // Find the standard test customer user created by the test framework
            var user = context.Users.FirstOrDefault(u =>
                u.Role == UserRole.Customer &&
                u.Email.Value == "customer@shopilent.com");

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Customer user not found. Ensure AuthenticateAsCustomerAsync() is called before this method, " +
                    "or the EnsureCustomerUserExistsAsync() has been executed.");
            }

            return user;
        });
    }

    /// <summary>
    /// Gets the standard test admin user used by AuthenticateAsAdminAsync.
    /// This user is created by the test framework with email "admin@shopilent.com".
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <returns>Admin User entity</returns>
    public static async Task<User> GetAdminUserAsync(
        Func<Func<ApplicationDbContext, Task<User>>, Task<User>> executeDbContext)
    {
        return await executeDbContext(async context =>
        {
            // Find the standard test admin user created by the test framework
            var user = context.Users.FirstOrDefault(u =>
                u.Role == UserRole.Admin &&
                u.Email.Value == "admin@shopilent.com");

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Admin user not found. Ensure AuthenticateAsAdminAsync() is called before this method, " +
                    "or the EnsureAdminUserExistsAsync() has been executed.");
            }

            return user;
        });
    }

    #endregion

    #region Shipping Domain Seeders

    /// <summary>
    /// Seeds a shipping address for a specific user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="user">User entity who owns the address</param>
    /// <param name="addressLine1">Address line 1 (auto-generated if null)</param>
    /// <param name="addressLine2">Address line 2 (optional)</param>
    /// <param name="city">City (auto-generated if null)</param>
    /// <param name="state">State/Province (auto-generated if null)</param>
    /// <param name="postalCode">Postal code (auto-generated if null)</param>
    /// <param name="country">Country (defaults to "United States")</param>
    /// <param name="phone">Phone number (auto-generated if null)</param>
    /// <param name="isDefault">Whether this is the default address (defaults to false)</param>
    /// <returns>Created and persisted Address entity</returns>
    public static async Task<Address> SeedAddressAsync(
        Func<Func<ApplicationDbContext, Task<Address>>, Task<Address>> executeDbContext,
        User user,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? country = null,
        string? phone = null,
        bool isDefault = false)
    {
        return await executeDbContext(async context =>
        {
            var line1 = addressLine1 ?? _faker.Address.StreetAddress();
            var cityName = city ?? _faker.Address.City();
            var stateName = state ?? _faker.Address.State();
            var zipCode = postalCode ?? _faker.Address.ZipCode();
            var countryName = country ?? "United States";

            var postalAddress = PostalAddress.Create(
                addressLine1: line1,
                city: cityName,
                state: stateName,
                country: countryName,
                postalCode: zipCode,
                addressLine2: addressLine2
            ).Value;

            var phoneNumber = phone != null ? PhoneNumber.Create(phone).Value : null;

            var address = Address.CreateShipping(
                userId: user.Id,
                postalAddress: postalAddress,
                phone: phoneNumber,
                isDefault: isDefault
            ).Value;

            context.Addresses.Add(address);
            await context.SaveChangesAsync();
            return address;
        });
    }

    #endregion

    #region Compound Seeders (Multiple Entities)

    /// <summary>
    /// Seeds a complete order scenario with user, addresses, products, and order.
    /// Useful for testing payment processing, order fulfillment, and shipping workflows.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="userEmail">Optional user email (auto-generated if null)</param>
    /// <param name="itemCount">Number of order items (defaults to 3)</param>
    /// <returns>Tuple containing User, Address, and Order entities</returns>
    public static async Task<(User User, Address ShippingAddress, Order Order)> SeedCompleteOrderScenarioAsync(
        Func<Func<ApplicationDbContext, Task<(User, Address, Order)>>, Task<(User, Address, Order)>> executeDbContext,
        string? userEmail = null,
        int itemCount = 3)
    {
        return await executeDbContext(async context =>
        {
            // Seed user
            var email = Email.Create(userEmail ?? _faker.Internet.Email()).Value;
            var fullName = FullName.Create(_faker.Name.FirstName(), _faker.Name.LastName()).Value;

            var user = User.Create(
                email: email,
                passwordHash: $"HASH-{Guid.NewGuid():N}",
                fullName: fullName,
                role: UserRole.Customer
            ).Value;
            user.VerifyEmail();

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Seed shipping address
            var postalAddress = PostalAddress.Create(
                addressLine1: _faker.Address.StreetAddress(),
                city: _faker.Address.City(),
                state: _faker.Address.State(),
                country: "United States",
                postalCode: _faker.Address.ZipCode()
            ).Value;

            var phoneNumber = PhoneNumber.Create(_faker.Phone.PhoneNumber()).Value;

            var shippingAddress = Address.CreateShipping(
                userId: user.Id,
                postalAddress: postalAddress,
                phone: phoneNumber,
                isDefault: true
            ).Value;

            context.Addresses.Add(shippingAddress);
            await context.SaveChangesAsync();

            // Create order with items
            var subtotal = 0m;

            for (int i = 0; i < itemCount; i++)
            {
                var productName = $"Product {i + 1} {Guid.NewGuid():N}";
                var productSlug = Slug.Create($"product-{i + 1}-{Guid.NewGuid():N}").Value;
                var productPrice = _faker.Random.Decimal(10, 200);
                var quantity = _faker.Random.Int(1, 3);

                var product = Product.CreateWithDescription(
                    name: productName,
                    slug: productSlug,
                    basePrice: Money.Create(productPrice, "USD").Value,
                    description: _faker.Commerce.ProductDescription(),
                    sku: $"SKU-{Guid.NewGuid():N}"
                ).Value;

                context.Products.Add(product);
                await context.SaveChangesAsync();

                var itemTotal = productPrice * quantity;
                subtotal += itemTotal;
            }

            var tax = Money.Create(subtotal * 0.08m, "USD").Value;
            var shippingCost = Money.Create(9.99m, "USD").Value;

            var order = Order.Create(
                user: user,
                shippingAddress: shippingAddress,
                billingAddress: shippingAddress,
                subtotal: Money.Create(subtotal, "USD").Value,
                tax: tax,
                shippingCost: shippingCost
            ).Value;

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            return (user, shippingAddress, order);
        });
    }

    /// <summary>
    /// Seeds a complete product catalog scenario with categories, products, and variants.
    /// Useful for testing catalog browsing, filtering, and search functionality.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="categoryCount">Number of categories to create (defaults to 3)</param>
    /// <param name="productsPerCategory">Number of products per category (defaults to 5)</param>
    /// <param name="variantsPerProduct">Number of variants per product (defaults to 2)</param>
    /// <returns>Tuple containing lists of Categories, Products, and ProductVariants</returns>
    public static async Task<(List<Category> Categories, List<Product> Products, List<ProductVariant> Variants)> SeedProductCatalogAsync(
        Func<Func<ApplicationDbContext, Task<(List<Category>, List<Product>, List<ProductVariant>)>>, Task<(List<Category>, List<Product>, List<ProductVariant>)>> executeDbContext,
        int categoryCount = 3,
        int productsPerCategory = 5,
        int variantsPerProduct = 2)
    {
        return await executeDbContext(async context =>
        {
            var categories = new List<Category>();
            var products = new List<Product>();
            var variants = new List<ProductVariant>();

            // Seed categories
            for (int i = 0; i < categoryCount; i++)
            {
                var categoryName = $"Category {i + 1} {Guid.NewGuid():N}";
                var categorySlug = Slug.Create($"category-{i + 1}-{Guid.NewGuid():N}").Value;

                var category = Category.Create(
                    name: categoryName,
                    slug: categorySlug,
                    parent: null
                ).Value;

                context.Categories.Add(category);
                await context.SaveChangesAsync();
                categories.Add(category);

                // Seed products for this category
                for (int j = 0; j < productsPerCategory; j++)
                {
                    var productName = $"Product {j + 1} in {categoryName}";
                    var productSlug = Slug.Create($"product-{j + 1}-cat-{i + 1}-{Guid.NewGuid():N}").Value;
                    var productPrice = Money.Create(_faker.Random.Decimal(10, 500), "USD").Value;

                    var product = Product.CreateWithDescription(
                        name: productName,
                        slug: productSlug,
                        basePrice: productPrice,
                        description: _faker.Commerce.ProductDescription(),
                        sku: $"SKU-{Guid.NewGuid():N}"
                    ).Value;

                    product.AddCategory(category);

                    context.Products.Add(product);
                    await context.SaveChangesAsync();
                    products.Add(product);

                    // Seed variants for this product
                    for (int k = 0; k < variantsPerProduct; k++)
                    {
                        var variantSku = $"VAR-{k + 1}-{Guid.NewGuid():N}";
                        var variantPrice = Money.Create(_faker.Random.Decimal(10, 450), "USD").Value;

                        var variantResult = ProductVariant.Create(
                            productId: product.Id,
                            sku: variantSku,
                            price: variantPrice,
                            stockQuantity: _faker.Random.Int(0, 200)
                        );

                        if (variantResult.IsSuccess)
                        {
                            var variant = variantResult.Value;
                            product.AddVariant(variant);
                            variants.Add(variant);
                        }
                    }

                    await context.SaveChangesAsync();
                }
            }

            return (categories, products, variants);
        });
    }

    #endregion

    #region Order Creation Test Helper Methods

    /// <summary>
    /// Seeds a cart with items from specific product IDs for authenticated user.
    /// Useful for order creation tests that need a pre-populated cart.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="productIds">Array of product IDs to add to cart</param>
    /// <param name="quantityPerItem">Quantity for each item (defaults to 1)</param>
    /// <returns>Created and persisted Cart entity with items</returns>
    public static async Task<Cart> SeedCartWithItemsAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        Guid[] productIds,
        int quantityPerItem = 1)
    {
        return await executeDbContext(async context =>
        {
            // Get or create the authenticated user's cart
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException(
                    "Customer user not found. Ensure AuthenticateAsCustomerAsync() is called before this method.");
            }

            // Create cart for user
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add each product to cart
            foreach (var productId in productIds)
            {
                var product = await context.Products.FindAsync(productId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product with ID {productId} not found.");
                }

                var addResult = cart.AddItem(product.Id, quantityPerItem, null);
                if (addResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
                }
            }

            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds a cart with items that have variants for authenticated user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="productId">Product ID that owns the variants</param>
    /// <param name="variantIds">Array of variant IDs to add to cart</param>
    /// <param name="quantityPerItem">Quantity for each item (defaults to 1)</param>
    /// <returns">Created and persisted Cart entity with variant items</returns>
    public static async Task<Cart> SeedCartWithVariantItemsAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        Guid productId,
        Guid[] variantIds,
        int quantityPerItem = 1)
    {
        return await executeDbContext(async context =>
        {
            // Get or create the authenticated user's cart
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException(
                    "Customer user not found. Ensure AuthenticateAsCustomerAsync() is called before this method.");
            }

            // Create cart for user
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Get product
            var product = await context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new InvalidOperationException($"Product with ID {productId} not found.");
            }

            // Add each variant to cart
            foreach (var variantId in variantIds)
            {
                var variant = await context.ProductVariants.FindAsync(variantId);
                if (variant == null)
                {
                    throw new InvalidOperationException($"Variant with ID {variantId} not found.");
                }

                var addResult = cart.AddItem(product.Id, quantityPerItem, variant?.Id);
                if (addResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to add variant item to cart: {addResult.Error}");
                }
            }

            await context.SaveChangesAsync();
            return cart;
        });
    }

    /// <summary>
    /// Seeds a cart for a different user (not the authenticated test user).
    /// Useful for testing cart ownership and access control.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <param name="productId">Product ID to add to the cart</param>
    /// <returns>Created and persisted Cart entity for different user</returns>
    public static async Task<Cart> SeedCartForDifferentUserAsync(
        Func<Func<ApplicationDbContext, Task<Cart>>, Task<Cart>> executeDbContext,
        Guid productId)
    {
        return await executeDbContext(async context =>
        {
            // Create a different user
            var email = Email.Create(_faker.Internet.Email()).Value;
            var fullName = FullName.Create(_faker.Name.FirstName(), _faker.Name.LastName()).Value;

            var user = User.Create(
                email: email,
                passwordHash: $"HASH-{Guid.NewGuid():N}",
                fullName: fullName,
                role: UserRole.Customer
            ).Value;
            user.VerifyEmail();

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Create cart for the different user
            var cart = Cart.Create(user.Id).Value;
            context.Carts.Add(cart);
            await context.SaveChangesAsync();

            // Add product to cart
            var product = await context.Products.FindAsync(productId);
            if (product != null)
            {
                cart.AddItem(product.Id, 1, null);
                await context.SaveChangesAsync();
            }

            return cart;
        });
    }

    /// <summary>
    /// Seeds an address for the authenticated customer user.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <returns>Created and persisted Address entity</returns>
    public static async Task<Address> SeedAddressAsync(
        Func<Func<ApplicationDbContext, Task<Address>>, Task<Address>> executeDbContext)
    {
        return await executeDbContext(async context =>
        {
            // Get the authenticated customer user
            var user = context.Users.FirstOrDefault(u => u.Email.Value == "customer@shopilent.com");
            if (user == null)
            {
                throw new InvalidOperationException(
                    "Customer user not found. Ensure AuthenticateAsCustomerAsync() is called before this method.");
            }

            // Create address
            var postalAddress = PostalAddress.Create(
                addressLine1: _faker.Address.StreetAddress(),
                city: _faker.Address.City(),
                state: _faker.Address.State(),
                country: "United States",
                postalCode: _faker.Address.ZipCode()
            ).Value;

            var phoneNumber = PhoneNumber.Create(_faker.Phone.PhoneNumber()).Value;

            var address = Address.CreateShipping(
                userId: user.Id,
                postalAddress: postalAddress,
                phone: phoneNumber,
                isDefault: false
            ).Value;

            context.Addresses.Add(address);
            await context.SaveChangesAsync();
            return address;
        });
    }

    /// <summary>
    /// Seeds an address for a different user (not the authenticated test user).
    /// Useful for testing address ownership and access control.
    /// </summary>
    /// <param name="executeDbContext">Database context execution delegate from test base</param>
    /// <returns>Created and persisted Address entity for different user</returns>
    public static async Task<Address> SeedAddressForDifferentUserAsync(
        Func<Func<ApplicationDbContext, Task<Address>>, Task<Address>> executeDbContext)
    {
        return await executeDbContext(async context =>
        {
            // Create a different user
            var email = Email.Create(_faker.Internet.Email()).Value;
            var fullName = FullName.Create(_faker.Name.FirstName(), _faker.Name.LastName()).Value;

            var user = User.Create(
                email: email,
                passwordHash: $"HASH-{Guid.NewGuid():N}",
                fullName: fullName,
                role: UserRole.Customer
            ).Value;
            user.VerifyEmail();

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Create address for the different user
            var postalAddress = PostalAddress.Create(
                addressLine1: _faker.Address.StreetAddress(),
                city: _faker.Address.City(),
                state: _faker.Address.State(),
                country: "United States",
                postalCode: _faker.Address.ZipCode()
            ).Value;

            var phoneNumber = PhoneNumber.Create(_faker.Phone.PhoneNumber()).Value;

            var address = Address.CreateShipping(
                userId: user.Id,
                postalAddress: postalAddress,
                phone: phoneNumber,
                isDefault: false
            ).Value;

            context.Addresses.Add(address);
            await context.SaveChangesAsync();
            return address;
        });
    }

    #endregion
}
