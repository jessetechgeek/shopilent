using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateVariantStatus.V1;

internal sealed class UpdateVariantStatusCommandHandlerV1 : ICommandHandler<UpdateVariantStatusCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateVariantStatusCommandHandlerV1> _logger;

    public UpdateVariantStatusCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductVariantWriteRepository productVariantWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateVariantStatusCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productVariantWriteRepository = productVariantWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateVariantStatusCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get variant by ID
            var variant = await _productVariantWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (variant == null)
            {
                return Result.Failure(
                    Error.NotFound(
                        code: "Variant.NotFound",
                        message: $"Variant with ID {request.Id} was not found."));
            }

            // Update status
            Result statusResult;
            if (request.IsActive)
            {
                statusResult = variant.Activate();
            }
            else
            {
                statusResult = variant.Deactivate();
            }

            if (statusResult.IsFailure)
            {
                return statusResult;
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                variant.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Variant status updated successfully. ID: {VariantId}, IsActive: {IsActive}",
                variant.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating variant status. ID: {VariantId}, IsActive: {IsActive}",
                request.Id, request.IsActive);

            return Result.Failure(
                Error.Failure(
                    code: "Variant.UpdateStatusFailed",
                    message: $"Failed to update variant status: {ex.Message}"));
        }
    }
}
