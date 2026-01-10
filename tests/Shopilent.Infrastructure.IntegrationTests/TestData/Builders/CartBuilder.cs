using Bogus;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Sales;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class CartBuilder
{
    private User _user;
    private readonly Dictionary<string, object> _metadata = new();
    private readonly List<(Product product, int quantity, ProductVariant variant)> _items = new();
    private readonly Faker _faker = new();

    public CartBuilder()
    {
    }

    public CartBuilder WithUser(User user)
    {
        _user = user;
        return this;
    }

    public CartBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public CartBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        foreach (var item in metadata)
        {
            _metadata[item.Key] = item.Value;
        }
        return this;
    }

    public CartBuilder WithItem(Product product, int quantity = 1, ProductVariant variant = null)
    {
        _items.Add((product, quantity, variant));
        return this;
    }

    public CartBuilder WithItems(params (Product product, int quantity, ProductVariant variant)[] items)
    {
        _items.AddRange(items);
        return this;
    }

    public CartBuilder WithRandomMetadata()
    {
        _metadata["source"] = _faker.PickRandom("web", "mobile", "api");
        _metadata["sessionId"] = _faker.Random.Guid().ToString();
        _metadata["userAgent"] = _faker.Internet.UserAgent();
        return this;
    }

    public CartBuilder WithRandomItems(int itemCount = 3)
    {
        for (int i = 0; i < itemCount; i++)
        {
            var product = new ProductBuilder().Build();
            var quantity = _faker.Random.Int(1, 5);
            _items.Add((product, quantity, null));
        }
        return this;
    }

    public CartBuilder AsAnonymousCart()
    {
        _user = null;
        return this;
    }

    public CartBuilder AsUserCart(User user = null)
    {
        _user = user ?? new UserBuilder().Build();
        return this;
    }

    public Cart Build()
    {
        Cart cart;

        // Check if we should use CreateWithMetadata (requires user) or regular Create
        if (_metadata.Any() && _user != null)
        {
            // Use CreateWithMetadata when we have both metadata and a user
            var cartResult = Cart.CreateWithMetadata(_user.Id, _metadata);
            if (cartResult.IsFailure)
                throw new InvalidOperationException($"Failed to create cart with metadata: {cartResult.Error}");
            cart = cartResult.Value;
        }
        else
        {
            // Create regular cart (can be anonymous or with user)
            var cartResult = Cart.Create(_user?.Id);
            if (cartResult.IsFailure)
                throw new InvalidOperationException($"Failed to create cart: {cartResult.Error}");
            cart = cartResult.Value;

            // If we have metadata but no user (anonymous cart), add metadata manually
            if (_metadata.Any())
            {
                foreach (var item in _metadata)
                {
                    cart.UpdateMetadata(item.Key, item.Value);
                }
            }
        }

        // Add items to cart
        foreach (var (product, quantity, variant) in _items)
        {
            var addResult = cart.AddItem(product.Id, quantity, variant?.Id);
            if (addResult.IsFailure)
                throw new InvalidOperationException($"Failed to add item to cart: {addResult.Error}");
        }

        return cart;
    }

    public static CartBuilder Default() => new CartBuilder();

    public static CartBuilder Anonymous() => new CartBuilder().AsAnonymousCart();

    public static CartBuilder WithTestUser(User user) => new CartBuilder().WithUser(user);

    public static CartBuilder WithRandomData() => new CartBuilder().WithRandomMetadata().WithRandomItems();

    public static CartBuilder Empty() => new CartBuilder();

    public static CartBuilder WithSingleItem(Product product, int quantity = 1, ProductVariant variant = null)
        => new CartBuilder().WithItem(product, quantity, variant);
}
