using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetAttributesDatatable.V1;

internal sealed class GetAttributesDatatableQueryHandlerV1 :
    IQueryHandler<GetAttributesDatatableQueryV1, DataTableResult<AttributeDatatableDto>>
{
    private readonly IAttributeReadRepository _attributeReadRepository;
    private readonly ILogger<GetAttributesDatatableQueryHandlerV1> _logger;

    public GetAttributesDatatableQueryHandlerV1(
        IAttributeReadRepository attributeReadRepository,
        ILogger<GetAttributesDatatableQueryHandlerV1> logger)
    {
        _attributeReadRepository = attributeReadRepository;
        _logger = logger;
    }

    public async Task<Result<DataTableResult<AttributeDatatableDto>>> Handle(
        GetAttributesDatatableQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _attributeReadRepository.GetDataTableAsync(
                request.Request,
                cancellationToken);

            // Map to AttributeDatatableDto
            var dtoItems = result.Data.Select(attribute => new AttributeDatatableDto
            {
                Id = attribute.Id,
                Name = attribute.Name,
                DisplayName = attribute.DisplayName,
                Type = attribute.Type.ToString(),
                Filterable = attribute.Filterable,
                Searchable = attribute.Searchable,
                IsVariant = attribute.IsVariant,
                Configuration = attribute.Configuration,
                CreatedAt = attribute.CreatedAt,
                UpdatedAt = attribute.UpdatedAt
            }).ToList();

            // Create new datatable result with mapped DTOs
            var datatableResult = new DataTableResult<AttributeDatatableDto>(
                result.Draw,
                result.RecordsTotal,
                result.RecordsFiltered,
                dtoItems);

            _logger.LogInformation("Retrieved {Count} attributes for datatable", dtoItems.Count);
            return Result.Success(datatableResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attributes for datatable");

            return Result.Failure<DataTableResult<AttributeDatatableDto>>(
                Error.Failure(
                    code: "Attributes.GetDataTableFailed",
                    message: $"Failed to retrieve attributes: {ex.Message}"));
        }
    }
}
