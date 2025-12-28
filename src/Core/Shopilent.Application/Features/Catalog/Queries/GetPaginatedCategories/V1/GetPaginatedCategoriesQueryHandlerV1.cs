using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetPaginatedCategories.V1;

internal sealed class GetPaginatedCategoriesQueryHandlerV1 :
    IQueryHandler<GetPaginatedCategoriesQueryV1, PaginatedResult<CategoryDto>>
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ILogger<GetPaginatedCategoriesQueryHandlerV1> _logger;

    public GetPaginatedCategoriesQueryHandlerV1(
        ICategoryReadRepository categoryReadRepository,
        ILogger<GetPaginatedCategoriesQueryHandlerV1> logger)
    {
        _categoryReadRepository = categoryReadRepository;
        _logger = logger;
    }

    public async Task<Result<PaginatedResult<CategoryDto>>> Handle(
        GetPaginatedCategoriesQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var paginatedResult = await _categoryReadRepository.GetPaginatedAsync(
                request.PageNumber,
                request.PageSize,
                request.SortColumn,
                request.SortDescending,
                cancellationToken);

            _logger.LogInformation(
                "Retrieved paginated categories: Page {PageNumber}, Size {PageSize}, Total {TotalCount}",
                paginatedResult.PageNumber,
                paginatedResult.PageSize,
                paginatedResult.TotalCount);

            return Result.Success(paginatedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated categories");

            return Result.Failure<PaginatedResult<CategoryDto>>(
                Error.Failure(
                    code: "Categories.GetPaginatedFailed",
                    message: $"Failed to retrieve paginated categories: {ex.Message}"));
        }
    }
}
