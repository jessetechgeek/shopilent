using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateCategoryStatus.V1;

internal sealed class UpdateCategoryStatusCommandHandlerV1 : ICommandHandler<UpdateCategoryStatusCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateCategoryStatusCommandHandlerV1> _logger;

    public UpdateCategoryStatusCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICategoryWriteRepository categoryWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateCategoryStatusCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _categoryWriteRepository = categoryWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateCategoryStatusCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get category by ID
            var category = await _categoryWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (category == null)
            {
                return Result.Failure(CategoryErrors.NotFound(request.Id));
            }

            // Update status
            Result statusResult;
            if (request.IsActive)
            {
                statusResult = category.Activate();
            }
            else
            {
                statusResult = category.Deactivate();
            }

            if (statusResult.IsFailure)
            {
                return statusResult;
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                category.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category status updated successfully. ID: {CategoryId}, IsActive: {IsActive}",
                category.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category status. ID: {CategoryId}, IsActive: {IsActive}",
                request.Id, request.IsActive);

            return Result.Failure(
                Error.Failure(
                    code: "Category.UpdateStatusFailed",
                    message: $"Failed to update category status: {ex.Message}"));
        }
    }
}
