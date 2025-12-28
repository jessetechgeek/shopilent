using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetChildCategories.V1;

internal sealed class
    GetChildCategoriesQueryHandlerV1 : IQueryHandler<GetChildCategoriesQueryV1, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ILogger<GetChildCategoriesQueryHandlerV1> _logger;

    public GetChildCategoriesQueryHandlerV1(
        ICategoryReadRepository categoryReadRepository,
        ILogger<GetChildCategoriesQueryHandlerV1> logger)
    {
        _categoryReadRepository = categoryReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetChildCategoriesQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify parent category exists
            var parentCategory = await _categoryReadRepository.GetByIdAsync(request.ParentId, cancellationToken);
            if (parentCategory == null)
            {
                return Result.Failure<IReadOnlyList<CategoryDto>>(CategoryErrors.NotFound(request.ParentId));
            }

            var childCategories =
                await _categoryReadRepository.GetChildCategoriesAsync(request.ParentId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} child categories for parent ID: {ParentId}",
                childCategories.Count, request.ParentId);

            return Result.Success(childCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child categories for parent ID: {ParentId}", request.ParentId);

            return Result.Failure<IReadOnlyList<CategoryDto>>(
                Error.Failure(
                    code: "Categories.GetChildCategoriesFailed",
                    message: $"Failed to retrieve child categories: {ex.Message}"));
        }
    }
}
