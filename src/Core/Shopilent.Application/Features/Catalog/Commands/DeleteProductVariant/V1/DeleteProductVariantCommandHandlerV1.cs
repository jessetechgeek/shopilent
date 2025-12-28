using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.DeleteProductVariant.V1;

internal sealed class DeleteProductVariantCommandHandlerV1 : ICommandHandler<DeleteProductVariantCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeleteProductVariantCommandHandlerV1> _logger;

    public DeleteProductVariantCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductVariantWriteRepository productVariantWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeleteProductVariantCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productVariantWriteRepository = productVariantWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteProductVariantCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get product variant by ID
            var variant = await _productVariantWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (variant == null)
            {
                return Result.Failure(Error.NotFound(
                    code: "ProductVariant.NotFound",
                    message: $"Product variant with ID {request.Id} was not found"
                ));
            }

            //TODO: Enable Soft Delete
            // Delete the variant
            await _productVariantWriteRepository.DeleteAsync(variant, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product variant with ID {VariantId} deleted successfully", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product variant with ID {VariantId}: {ErrorMessage}",
                request.Id, ex.Message);

            return Result.Failure(
                Error.Failure(
                    code: "ProductVariant.DeleteFailed",
                    message: "Failed to delete product variant"
                ));
        }
    }
}
