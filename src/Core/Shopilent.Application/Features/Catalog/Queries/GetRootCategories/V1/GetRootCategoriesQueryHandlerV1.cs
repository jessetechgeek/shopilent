using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetRootCategories.V1;

internal sealed class GetRootCategoriesQueryHandlerV1 : IQueryHandler<GetRootCategoriesQueryV1, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ILogger<GetRootCategoriesQueryHandlerV1> _logger;

    public GetRootCategoriesQueryHandlerV1(
        ICategoryReadRepository categoryReadRepository,
        ILogger<GetRootCategoriesQueryHandlerV1> logger)
    {
        _categoryReadRepository = categoryReadRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetRootCategoriesQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rootCategories = await _categoryReadRepository.GetRootCategoriesAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} root categories", rootCategories.Count);
            return Result.Success(rootCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving root categories");

            return Result.Failure<IReadOnlyList<CategoryDto>>(
                Error.Failure(
                    code: "Categories.GetRootCategoriesFailed",
                    message: $"Failed to retrieve root categories: {ex.Message}"));
        }
    }
}
