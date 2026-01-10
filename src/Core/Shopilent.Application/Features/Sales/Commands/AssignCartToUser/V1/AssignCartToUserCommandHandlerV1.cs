using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.AssignCartToUser.V1;

internal sealed class AssignCartToUserCommandHandlerV1 : ICommandHandler<AssignCartToUserCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AssignCartToUserCommandHandlerV1> _logger;

    public AssignCartToUserCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        ICartWriteRepository cartWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<AssignCartToUserCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _cartWriteRepository = cartWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(AssignCartToUserCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure user is authenticated
            if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
            {
                return Result.Failure(Error.Unauthorized(
                    code: "Cart.UserNotAuthenticated",
                    message: "User must be authenticated to assign cart"));
            }

            var userId = _currentUserContext.UserId.Value;

            // Get the cart by ID
            var cart = await _cartWriteRepository.GetByIdAsync(request.CartId, cancellationToken);
            if (cart == null)
            {
                return Result.Failure(CartErrors.CartNotFound(request.CartId));
            }

            // Check if cart is already assigned to a user
            if (cart.UserId.HasValue)
            {
                // If it's already assigned to the current user, return success
                if (cart.UserId.Value == userId)
                {
                    _logger.LogInformation("Cart {CartId} is already assigned to user {UserId}",
                        request.CartId, userId);
                    return Result.Success();
                }

                // If it's assigned to a different user, return an error
                return Result.Failure(Error.Validation(
                    code: "Cart.AlreadyAssigned",
                    message: "Cart is already assigned to another user"));
            }

            // Get the user to assign the cart to
            var user = await _userWriteRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return Result.Failure(UserErrors.NotFound(userId));
            }

            // Check if user already has a cart
            var existingUserCart = await _cartWriteRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existingUserCart != null)
            {
                // If user already has a cart, we could merge them or return an error
                // For now, let's return an error to keep it simple
                return Result.Failure(Error.Validation(
                    code: "Cart.UserAlreadyHasCart",
                    message: "User already has an assigned cart. Please merge or clear existing cart first."));
            }

            // Assign the cart to the user
            var assignResult = cart.AssignToUser(user.Id);
            if (assignResult.IsFailure)
            {
                _logger.LogWarning("Failed to assign cart {CartId} to user {UserId}: {Error}",
                    request.CartId, userId, assignResult.Error.Message);
                return assignResult;
            }

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Cart {CartId} successfully assigned to user {UserId}",
                request.CartId, userId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning cart {CartId} to user: {ErrorMessage}",
                request.CartId, ex.Message);

            return Result.Failure(
                Error.Failure(
                    code: "Cart.AssignmentFailed",
                    message: $"Failed to assign cart to user: {ex.Message}"));
        }
    }
}
