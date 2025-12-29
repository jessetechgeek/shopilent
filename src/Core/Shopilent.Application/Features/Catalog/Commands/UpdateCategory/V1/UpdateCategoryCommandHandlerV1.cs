using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateCategory.V1;

internal sealed class UpdateCategoryCommandHandlerV1 : ICommandHandler<UpdateCategoryCommandV1, UpdateCategoryResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateCategoryCommandHandlerV1> _logger;

    public UpdateCategoryCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICategoryWriteRepository categoryWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateCategoryCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _categoryWriteRepository = categoryWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateCategoryResponseV1>> Handle(UpdateCategoryCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get category by ID
            var category = await _categoryWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (category == null)
            {
                return Result.Failure<UpdateCategoryResponseV1>(CategoryErrors.NotFound(request.Id));
            }

            // Check if slug already exists (but exclude current category)
            if (category.Slug.Value != request.Slug)
            {
                var slugExists = await _categoryWriteRepository.SlugExistsAsync(request.Slug, request.Id, cancellationToken);
                if (slugExists)
                {
                    return Result.Failure<UpdateCategoryResponseV1>(CategoryErrors.DuplicateSlug(request.Slug));
                }
            }

            // Create slug value object
            var slugResult = Slug.Create(request.Slug);
            if (slugResult.IsFailure)
            {
                return Result.Failure<UpdateCategoryResponseV1>(slugResult.Error);
            }

            // Update category
            var updateResult = category.Update(request.Name, slugResult.Value, request.Description);
            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateCategoryResponseV1>(updateResult.Error);
            }

            // Update active status if specified
            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value && !category.IsActive)
                {
                    category.Activate();
                }
                else if (!request.IsActive.Value && category.IsActive)
                {
                    category.Deactivate();
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                category.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            // Create response
            var response = new UpdateCategoryResponseV1
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug.Value,
                Description = category.Description,
                ParentId = category.ParentId,
                Level = category.Level,
                Path = category.Path,
                IsActive = category.IsActive,
                UpdatedAt = category.UpdatedAt
            };

            _logger.LogInformation("Category updated successfully with ID: {CategoryId}", category.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category with ID {CategoryId}: {ErrorMessage}", request.Id, ex.Message);

            return Result.Failure<UpdateCategoryResponseV1>(
                Error.Failure(
                    "Category.UpdateFailed",
                    Domain.Common.Errors.ErrorType.Failure.ToString()
                ));
        }
    }
}
