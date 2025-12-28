using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetAttribute.V1;

internal sealed class GetAttributeQueryHandlerV1 : IQueryHandler<GetAttributeQueryV1, AttributeDto>
{
    private readonly IAttributeReadRepository _attributeReadRepository;
    private readonly ILogger<GetAttributeQueryHandlerV1> _logger;

    public GetAttributeQueryHandlerV1(
        IAttributeReadRepository attributeReadRepository,
        ILogger<GetAttributeQueryHandlerV1> logger)
    {
        _attributeReadRepository = attributeReadRepository;
        _logger = logger;
    }

    public async Task<Result<AttributeDto>> Handle(GetAttributeQueryV1 request, CancellationToken cancellationToken)
    {
        try
        {
            var attribute = await _attributeReadRepository.GetByIdAsync(request.Id, cancellationToken);

            if (attribute == null)
            {
                _logger.LogWarning("Attribute with ID {AttributeId} was not found", request.Id);
                return Result.Failure<AttributeDto>(AttributeErrors.NotFound(request.Id));
            }

            _logger.LogInformation("Retrieved attribute with ID {AttributeId}", request.Id);
            return Result.Success(attribute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attribute with ID {AttributeId}", request.Id);

            return Result.Failure<AttributeDto>(
                Error.Failure(
                    code: "Attribute.GetFailed",
                    message: $"Failed to retrieve attribute: {ex.Message}"));
        }
    }
}
