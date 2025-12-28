using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetAllAttributes.V1;

internal sealed class
    GetAllAttributesQueryHandlerV1 : IQueryHandler<GetAllAttributesQueryV1, IReadOnlyList<AttributeDto>>
{
    private readonly IAttributeReadRepository _attributeReadRepository;
    private readonly ILogger<GetAllAttributesQueryHandlerV1> _logger;

    public GetAllAttributesQueryHandlerV1(
        IAttributeReadRepository attributeReadRepository,
        ILogger<GetAllAttributesQueryHandlerV1> logger)
    {
        _attributeReadRepository = attributeReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<AttributeDto>>> Handle(
        GetAllAttributesQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var attributes = await _attributeReadRepository.ListAllAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} attributes", attributes.Count);
            return Result.Success(attributes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all attributes");

            return Result.Failure<IReadOnlyList<AttributeDto>>(
                Error.Failure(
                    code: "Attributes.GetAllFailed",
                    message: $"Failed to retrieve attributes: {ex.Message}"));
        }
    }
}
