using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.UpdateCartItemQuantity.V1;

internal sealed class
    UpdateCartItemQuantityCommandHandlerV1 : ICommandHandler<UpdateCartItemQuantityCommandV1,
    UpdateCartItemQuantityResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateCartItemQuantityCommandHandlerV1> _logger;

    public UpdateCartItemQuantityCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICartWriteRepository cartWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateCartItemQuantityCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _cartWriteRepository = cartWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateCartItemQuantityResponseV1>> Handle(
        UpdateCartItemQuantityCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the cart by cart item ID (works for both authenticated and anonymous users)
            var cart = await _cartWriteRepository.GetCartByItemIdAsync(request.CartItemId, cancellationToken);

            if (cart == null)
            {
                return Result.Failure<UpdateCartItemQuantityResponseV1>(CartErrors.CartNotFound(Guid.Empty));
            }

            // For authenticated users, verify the cart belongs to them
            if (_currentUserContext.UserId.HasValue && cart.UserId != _currentUserContext.UserId.Value)
            {
                return Result.Failure<UpdateCartItemQuantityResponseV1>(CartErrors.CartNotFound(Guid.Empty));
            }

            // Update the cart item quantity
            var updateResult = cart.UpdateItemQuantity(request.CartItemId, request.Quantity);
            if (updateResult.IsFailure)
            {
                return Result.Failure<UpdateCartItemQuantityResponseV1>(updateResult.Error);
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Cart item quantity updated successfully. CartId: {CartId}, ItemId: {ItemId}, Quantity: {Quantity}, UserId: {UserId}",
                cart.Id, request.CartItemId, request.Quantity, cart.UserId);

            return Result.Success(new UpdateCartItemQuantityResponseV1
            {
                CartItemId = request.CartItemId, Quantity = request.Quantity, UpdatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item quantity. ItemId: {ItemId}", request.CartItemId);
            return Result.Failure<UpdateCartItemQuantityResponseV1>(
                Error.Failure(
                    code: "UpdateCartItemQuantity.Failed",
                    message: $"Failed to update cart item quantity: {ex.Message}"));
        }
    }
}
