using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateProductStatus.V1;

internal sealed class UpdateProductStatusCommandHandlerV1 : ICommandHandler<UpdateProductStatusCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateProductStatusCommandHandlerV1> _logger;

    public UpdateProductStatusCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductWriteRepository productWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateProductStatusCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productWriteRepository = productWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateProductStatusCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get product by ID
            var product = await _productWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (product == null)
            {
                return Result.Failure(ProductErrors.NotFound(request.Id));
            }

            // Update status
            Result statusResult;
            if (request.IsActive)
            {
                statusResult = product.Activate();
            }
            else
            {
                statusResult = product.Deactivate();
            }

            if (statusResult.IsFailure)
            {
                return statusResult;
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                product.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Product status updated successfully. ID: {ProductId}, IsActive: {IsActive}",
                product.Id, request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product status. ID: {ProductId}, IsActive: {IsActive}",
                request.Id, request.IsActive);

            return Result.Failure(
                Error.Failure(
                    code: "Product.UpdateStatusFailed",
                    message: $"Failed to update product status: {ex.Message}"));
        }
    }
}
