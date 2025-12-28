using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateVariantStock.V1;

internal sealed class
    UpdateVariantStockCommandHandlerV1 : ICommandHandler<UpdateVariantStockCommandV1, UpdateVariantStockResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateVariantStockCommandHandlerV1> _logger;

    public UpdateVariantStockCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductVariantWriteRepository productVariantWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateVariantStockCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productVariantWriteRepository = productVariantWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateVariantStockResponseV1>> Handle(UpdateVariantStockCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the variant
            var variant = await _productVariantWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (variant == null)
            {
                return Result.Failure<UpdateVariantStockResponseV1>(
                    Error.NotFound("Variant.NotFound", $"Variant with ID {request.Id} not found"));
            }

            // Update the stock quantity
            var updateResult = variant.SetStockQuantity(request.StockQuantity);
            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateVariantStockResponseV1>(updateResult.Error);
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                variant.SetAuditInfo(_currentUserContext.UserId);
            }

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Create response
            var response = new UpdateVariantStockResponseV1
            {
                Id = variant.Id,
                StockQuantity = variant.StockQuantity,
                IsActive = variant.IsActive,
                UpdatedAt = variant.UpdatedAt
            };

            _logger.LogInformation(
                "Variant stock updated successfully with ID: {VariantId}, New Stock: {StockQuantity}",
                variant.Id, variant.StockQuantity);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating variant stock with ID {VariantId}: {ErrorMessage}",
                request.Id, ex.Message);

            return Result.Failure<UpdateVariantStockResponseV1>(
                Error.Failure(
                    "Variant.UpdateStockFailed",
                    ErrorType.Failure.ToString()
                ));
        }
    }
}
