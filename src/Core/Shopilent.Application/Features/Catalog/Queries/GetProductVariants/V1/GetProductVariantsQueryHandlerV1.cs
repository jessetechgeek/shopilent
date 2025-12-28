using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Queries.GetProductVariants.V1;

internal sealed class
    GetProductVariantsQueryHandlerV1 : IQueryHandler<GetProductVariantsQueryV1, IReadOnlyList<ProductVariantDto>>
{
    private readonly IProductReadRepository _productReader;
    private readonly IProductVariantReadRepository _productVariantReader;
    private readonly ILogger<GetProductVariantsQueryHandlerV1> _logger;

    public GetProductVariantsQueryHandlerV1(
        IProductReadRepository productReader,
        IProductVariantReadRepository productVariantReader,
        ILogger<GetProductVariantsQueryHandlerV1> logger)
    {
        _productReader = productReader;
        _productVariantReader = productVariantReader;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ProductVariantDto>>> Handle(
        GetProductVariantsQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify the product exists
            var product = await _productReader.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} was not found", request.ProductId);
                return Result.Failure<IReadOnlyList<ProductVariantDto>>(
                    Error.NotFound(message: $"Product with ID {request.ProductId} not found"));
            }

            // Get product variants
            var variants = await _productVariantReader.GetByProductIdAsync(request.ProductId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} variants for product with ID {ProductId}",
                variants.Count, request.ProductId);

            return Result.Success(variants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving variants for product with ID {ProductId}", request.ProductId);

            return Result.Failure<IReadOnlyList<ProductVariantDto>>(
                Error.Failure(
                    code: "ProductVariants.GetFailed",
                    message: $"Failed to retrieve product variants: {ex.Message}"));
        }
    }
}
