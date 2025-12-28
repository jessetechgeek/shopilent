using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetCategory.V1;

internal sealed class GetCategoryQueryHandlerV1 : IQueryHandler<GetCategoryQueryV1, CategoryDto>
{
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ILogger<GetCategoryQueryHandlerV1> _logger;

    public GetCategoryQueryHandlerV1(
        ICategoryReadRepository categoryReadRepository,
        ILogger<GetCategoryQueryHandlerV1> logger)
    {
        _categoryReadRepository = categoryReadRepository;
        _logger = logger;
    }

    public async Task<Result<CategoryDto>> Handle(GetCategoryQueryV1 request, CancellationToken cancellationToken)
    {
        try
        {
            var category = await _categoryReadRepository.GetByIdAsync(request.Id, cancellationToken);

            if (category == null)
            {
                _logger.LogWarning("Category with ID {CategoryId} was not found", request.Id);
                return Result.Failure<CategoryDto>(CategoryErrors.NotFound(request.Id));
            }

            _logger.LogInformation("Retrieved category with ID {CategoryId}", request.Id);
            return Result.Success(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category with ID {CategoryId}", request.Id);

            return Result.Failure<CategoryDto>(
                Error.Failure(
                    code: "Category.GetFailed",
                    message: $"Failed to retrieve category: {ex.Message}"));
        }
    }
}
