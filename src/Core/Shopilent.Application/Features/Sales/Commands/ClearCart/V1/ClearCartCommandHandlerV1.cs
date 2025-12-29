using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.ClearCart.V1;

internal sealed class ClearCartCommandHandlerV1 : ICommandHandler<ClearCartCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ClearCartCommandHandlerV1> _logger;

    public ClearCartCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICartWriteRepository cartWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<ClearCartCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _cartWriteRepository = cartWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(ClearCartCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            Cart cart = null;

            // Strategy 1: If CartId is provided, get cart by ID (supports anonymous users)
            if (request.CartId.HasValue)
            {
                cart = await _cartWriteRepository.GetByIdAsync(request.CartId.Value, cancellationToken);

                if (cart == null)
                {
                    return Result.Failure(CartErrors.CartNotFound(request.CartId.Value));
                }

                // For authenticated users, verify the cart belongs to them (security check)
                if (_currentUserContext.UserId.HasValue && cart.UserId.HasValue &&
                    cart.UserId.Value != _currentUserContext.UserId.Value)
                {
                    _logger.LogWarning(
                        "User {UserId} attempted to clear cart {CartId} that belongs to user {CartUserId}",
                        _currentUserContext.UserId.Value, cart.Id, cart.UserId.Value);
                    return Result.Failure(CartErrors.CartNotFound(request.CartId.Value));
                }
            }
            // Strategy 2: For authenticated users without CartId, get their cart
            else if (_currentUserContext.UserId.HasValue)
            {
                cart = await _cartWriteRepository.GetByUserIdAsync(_currentUserContext.UserId.Value,
                    cancellationToken);

                if (cart == null)
                {
                    return Result.Failure(CartErrors.CartNotFound(Guid.Empty));
                }
            }
            // Strategy 3: Anonymous user without CartId - cannot proceed
            else
            {
                _logger.LogWarning("Attempt to clear cart for anonymous user without cart ID");
                return Result.Failure(CartErrors.CartNotFound(Guid.Empty));
            }

            // Clear the cart
            var clearResult = cart.Clear();
            if (clearResult.IsFailure)
            {
                _logger.LogWarning("Failed to clear cart {CartId}: {Error}",
                    cart.Id, clearResult.Error.Message);
                return clearResult;
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Cart {CartId} successfully cleared for user {UserId}",
                cart.Id, cart.UserId ?? Guid.Empty);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart: {ErrorMessage}", ex.Message);

            return Result.Failure(
                Error.Failure(
                    code: "Cart.ClearFailed",
                    message: $"Failed to clear cart: {ex.Message}"));
        }
    }
}
