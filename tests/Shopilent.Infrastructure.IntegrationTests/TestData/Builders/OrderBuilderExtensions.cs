using Shopilent.Domain.Shipping.Repositories.Write;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public static class OrderBuilderExtensions
{
    /// <summary>
    /// Persists all addresses created by the OrderBuilder to the database.
    /// Call this AFTER building the order but BEFORE saving the order.
    /// </summary>
    public static async Task PersistAddressesAsync(
        this OrderBuilder orderBuilder,
        IAddressWriteRepository addressWriteRepository,
        CancellationToken cancellationToken = default)
    {
        if (orderBuilder.ShippingAddress != null)
        {
            await addressWriteRepository.AddAsync(orderBuilder.ShippingAddress, cancellationToken);
        }

        // Only add billing address if it's different from shipping
        if (orderBuilder.BillingAddress != null &&
            orderBuilder.BillingAddress.Id != orderBuilder.ShippingAddress?.Id)
        {
            await addressWriteRepository.AddAsync(orderBuilder.BillingAddress, cancellationToken);
        }
    }
}
