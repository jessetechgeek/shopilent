using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetCategoriesDatatable.V1;

internal sealed class GetCategoriesDatatableQueryHandlerV1 :
    IQueryHandler<GetCategoriesDatatableQueryV1, DataTableResult<CategoryDatatableDto>>
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ILogger<GetCategoriesDatatableQueryHandlerV1> _logger;

    public GetCategoriesDatatableQueryHandlerV1(
        ICategoryReadRepository categoryReadRepository,
        ILogger<GetCategoriesDatatableQueryHandlerV1> logger)
    {
        _categoryReadRepository = categoryReadRepository;
        _logger = logger;
    }

    public async Task<Result<DataTableResult<CategoryDatatableDto>>> Handle(
        GetCategoriesDatatableQueryV1 request,
        CancellationToken cancellationToken)
    {
        if (request.Request == null)
        {
            // Let this exception propagate to the caller
            throw new ArgumentNullException(nameof(request.Request), "DataTable request cannot be null");
        }

        try
        {
            // Get datatable results from repository
            var result = await _categoryReadRepository.GetCategoryDetailDataTableAsync(
                request.Request,
                cancellationToken);

            // Map to CategoryDatatableDto
            var dtoItems = result.Data.Select(category => new CategoryDatatableDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ParentId = category.ParentId,
                ParentName = category.ParentName, // Now using the ParentName from CategoryDetailDto
                Level = category.Level,
                IsActive = category.IsActive,
                ProductCount = category.ProductCount, // Now using the ProductCount from CategoryDetailDto
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            }).ToList();

            // Create new datatable result with mapped DTOs
            var datatableResult = new DataTableResult<CategoryDatatableDto>(
                result.Draw,
                result.RecordsTotal,
                result.RecordsFiltered,
                dtoItems);

            _logger.LogInformation("Retrieved {Count} categories for datatable", dtoItems.Count);
            return Result.Success(datatableResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories for datatable");

            return Result.Failure<DataTableResult<CategoryDatatableDto>>(
                Error.Failure(
                    code: "Categories.GetDataTableFailed",
                    message: $"Failed to retrieve categories: {ex.Message}"));
        }
    }
}
