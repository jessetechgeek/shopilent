using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.DeleteProduct.V1;

internal sealed class DeleteProductCommandHandlerV1 : ICommandHandler<DeleteProductCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeleteProductCommandHandlerV1> _logger;

    public DeleteProductCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductWriteRepository productWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeleteProductCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productWriteRepository = productWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteProductCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get product by ID
            var product = await _productWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (product == null)
            {
                return Result.Failure(ProductErrors.NotFound(request.Id));
            }

            //TODO: Enable Soft Delete
            // Delete the product
            await _productWriteRepository.DeleteAsync(product, cancellationToken);

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Product deleted successfully with ID: {ProductId}", product.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product with ID {ProductId}: {ErrorMessage}", request.Id,
                ex.Message);

            return Result.Failure(
                Error.Failure(
                    "Product.DeleteFailed",
                    "Failed to delete product"
                ));
        }
    }
}
