using Shopilent.Domain.Common.Repositories.Read;
using Shopilent.Domain.Shipping.DTOs;
using Shopilent.Domain.Shipping.Enums;

namespace Shopilent.Domain.Shipping.Repositories.Read;

public interface IAddressReadRepository : IAggregateReadRepository<AddressDto>
{
    Task<IReadOnlyList<AddressDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AddressDto> GetDefaultAddressAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressDto>> GetByAddressTypeAsync(Guid userId, AddressType addressType,
        CancellationToken cancellationToken = default);
}