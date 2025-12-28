using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateCategoryParent.V1;

internal sealed class UpdateCategoryParentCommandHandlerV1 : ICommandHandler<UpdateCategoryParentCommandV1, UpdateCategoryParentResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateCategoryParentCommandHandlerV1> _logger;

    public UpdateCategoryParentCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICategoryWriteRepository categoryWriteRepository,
        ICategoryReadRepository categoryReadRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateCategoryParentCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _categoryWriteRepository = categoryWriteRepository;
        _categoryReadRepository = categoryReadRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateCategoryParentResponseV1>> Handle(UpdateCategoryParentCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get category by ID
            var category = await _categoryWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (category == null)
            {
                return Result.Failure<UpdateCategoryParentResponseV1>(CategoryErrors.NotFound(request.Id));
            }

            // Get parent category if provided
            if (request.ParentId.HasValue)
            {
                var parentCategory = await _categoryWriteRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
                if (parentCategory == null)
                {
                    return Result.Failure<UpdateCategoryParentResponseV1>(CategoryErrors.NotFound(request.ParentId.Value));
                }

                // Set parent
                var setParentResult = category.SetParent(parentCategory);
                if (setParentResult.IsFailure)
                {
                    return Result.Failure<UpdateCategoryParentResponseV1>(setParentResult.Error);
                }
            }
            else
            {
                // Remove parent (set as root category)
                var setParentResult = category.SetParent(null);
                if (setParentResult.IsFailure)
                {
                    return Result.Failure<UpdateCategoryParentResponseV1>(setParentResult.Error);
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                category.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Get parent name if applicable
            string parentName = null;
            if (category.ParentId.HasValue)
            {
                var parentCategory = await _categoryReadRepository.GetByIdAsync(category.ParentId.Value, cancellationToken);
                parentName = parentCategory?.Name;
            }

            // Create response
            var response = new UpdateCategoryParentResponseV1
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug.Value,
                ParentId = category.ParentId,
                ParentName = parentName,
                Level = category.Level,
                Path = category.Path,
                UpdatedAt = category.UpdatedAt
            };

            _logger.LogInformation("Category parent updated successfully with ID: {CategoryId}", category.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category parent with ID {CategoryId}: {ErrorMessage}", request.Id, ex.Message);

            return Result.Failure<UpdateCategoryParentResponseV1>(
                Error.Failure(
                    "Category.UpdateParentFailed",
                    $"Failed to update category parent: {ex.Message}"
                ));
        }
    }
}
