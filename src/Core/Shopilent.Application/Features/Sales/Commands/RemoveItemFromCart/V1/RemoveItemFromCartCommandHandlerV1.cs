using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.RemoveItemFromCart.V1;

internal sealed class RemoveItemFromCartCommandHandlerV1 : ICommandHandler<RemoveItemFromCartCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<RemoveItemFromCartCommandHandlerV1> _logger;

    public RemoveItemFromCartCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICartWriteRepository cartWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<RemoveItemFromCartCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _cartWriteRepository = cartWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveItemFromCartCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the cart by cart item ID (works for both authenticated and anonymous users)
            var cart = await _cartWriteRepository.GetCartByItemIdAsync(request.ItemId, cancellationToken);

            if (cart == null)
            {
                return Result.Failure(CartErrors.CartNotFound(Guid.Empty));
            }

            // For authenticated users, verify the cart belongs to them
            if (_currentUserContext.UserId.HasValue && cart.UserId != _currentUserContext.UserId.Value)
            {
                return Result.Failure(CartErrors.CartNotFound(Guid.Empty));
            }

            // Remove item from cart
            var removeResult = cart.RemoveItem(request.ItemId);
            if (removeResult.IsFailure)
            {
                _logger.LogWarning("Failed to remove item {ItemId} from cart {CartId}: {Error}",
                    request.ItemId, cart.Id, removeResult.Error.Message);
                return removeResult;
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Item {ItemId} successfully removed from cart {CartId} for user {UserId}",
                request.ItemId, cart.Id, cart.UserId ?? Guid.Empty);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from cart: {ErrorMessage}",
                request.ItemId, ex.Message);

            return Result.Failure(
                Error.Failure(
                    code: "Cart.RemoveItemFailed",
                    message: $"Failed to remove item from cart: {ex.Message}"));
        }
    }
}
