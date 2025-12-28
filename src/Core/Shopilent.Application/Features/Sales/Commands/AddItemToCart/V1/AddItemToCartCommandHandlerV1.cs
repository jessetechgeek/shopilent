using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.AddItemToCart.V1;

internal sealed class AddItemToCartCommandHandlerV1 : ICommandHandler<AddItemToCartCommandV1, AddItemToCartResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IProductVariantWriteRepository _productVariantWriteRepository;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AddItemToCartCommandHandlerV1> _logger;

    public AddItemToCartCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserWriteRepository userWriteRepository,
        IProductVariantWriteRepository productVariantWriteRepository,
        ICartWriteRepository cartWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<AddItemToCartCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userWriteRepository = userWriteRepository;
        _productVariantWriteRepository = productVariantWriteRepository;
        _cartWriteRepository = cartWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<AddItemToCartResponseV1>> Handle(AddItemToCartCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get current user if authenticated
            User? user = null;
            if (_currentUserContext.IsAuthenticated && _currentUserContext.UserId.HasValue)
            {
                user = await _userWriteRepository.GetByIdAsync(_currentUserContext.UserId.Value, cancellationToken);
            }

            // Get cart by ID if specified, otherwise get or create user's cart
            Cart? cart = null;

            if (request.CartId.HasValue)
            {
                // Get cart by specified ID
                cart = await _cartWriteRepository.GetByIdAsync(request.CartId.Value, cancellationToken);
                if (cart == null)
                {
                    _logger.LogWarning("Cart not found. CartId: {CartId}", request.CartId);
                    return Result.Failure<AddItemToCartResponseV1>(CartErrors.CartNotFound(request.CartId.Value));
                }

                // If user is authenticated, verify cart ownership or assign cart to user
                if (user != null)
                {
                    if (cart.UserId == null)
                    {
                        // Assign anonymous cart to authenticated user
                        var assignResult = cart.AssignToUser(user);
                        if (assignResult.IsFailure)
                        {
                            _logger.LogError("Failed to assign cart to user: {Error}", assignResult.Error);
                            return Result.Failure<AddItemToCartResponseV1>(assignResult.Error);
                        }

                        // Update cart with user assignment
                        await _cartWriteRepository.UpdateAsync(cart, cancellationToken);
                    }
                    else if (cart.UserId != user.Id)
                    {
                        // Cart belongs to different user
                        _logger.LogWarning(
                            "Cart belongs to different user. CartId: {CartId}, UserId: {UserId}, CartUserId: {CartUserId}",
                            request.CartId, user.Id, cart.UserId);
                        return Result.Failure<AddItemToCartResponseV1>(
                            Error.Forbidden("Cart.AccessDenied", "You don't have access to this cart"));
                    }
                }
            }
            else
            {
                // Get or create cart for authenticated user
                if (user != null)
                {
                    cart = await _cartWriteRepository.GetByUserIdAsync(user.Id, cancellationToken);
                }

                if (cart == null)
                {
                    var cartResult = Cart.Create(user);
                    if (cartResult.IsFailure)
                    {
                        _logger.LogError("Failed to create cart: {Error}", cartResult.Error);
                        return Result.Failure<AddItemToCartResponseV1>(cartResult.Error);
                    }

                    cart = cartResult.Value;
                    await _cartWriteRepository.AddAsync(cart, cancellationToken);

                    // Save the new cart to database first
                    var saveCartResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
                    if (saveCartResult == 0)
                    {
                        _logger.LogError("Failed to save new cart to database");
                        return Result.Failure<AddItemToCartResponseV1>(
                            Error.Failure("Cart.CreateFailed", "Failed to create cart"));
                    }

                    _logger.LogInformation("New cart created and saved. CartId: {CartId}", cart.Id);
                }
            }

            // Get product
            var product = await _unitOfWork.ProductWriter.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
            {
                _logger.LogWarning("Product not found. ProductId: {ProductId}", request.ProductId);
                return Result.Failure<AddItemToCartResponseV1>(ProductErrors.NotFound(request.ProductId));
            }

            // Get variant if specified
            ProductVariant? variant = null;
            if (request.VariantId.HasValue)
            {
                variant = await _productVariantWriteRepository.GetByIdAsync(request.VariantId.Value,
                    cancellationToken);
                if (variant == null)
                {
                    _logger.LogWarning("Product variant not found. VariantId: {VariantId}", request.VariantId);
                    return Result.Failure<AddItemToCartResponseV1>(
                        CartErrors.ProductVariantNotFound(request.VariantId.Value));
                }
            }

            // Add item to cart
            var addItemResult = cart.AddItem(product, request.Quantity, variant);
            if (addItemResult.IsFailure)
            {
                _logger.LogError("Failed to add item to cart: {Error}", addItemResult.Error);
                return Result.Failure<AddItemToCartResponseV1>(addItemResult.Error);
            }

            // Save changes (final save for cart item addition)
            await _cartWriteRepository.UpdateAsync(cart, cancellationToken);
            var finalSaveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (finalSaveResult == 0)
            {
                _logger.LogError("Failed to save cart item changes. CartId: {CartId}", cart.Id);
                return Result.Failure<AddItemToCartResponseV1>(
                    Error.Failure("Cart.SaveFailed", "Failed to save cart changes"));
            }

            _logger.LogInformation(
                "Item added to cart successfully. CartId: {CartId}, ProductId: {ProductId}, Quantity: {Quantity}",
                cart.Id, request.ProductId, request.Quantity);

            var cartItem = addItemResult.Value;
            var response = new AddItemToCartResponseV1
            {
                CartId = cart.Id,
                CartItemId = cartItem.Id,
                ProductId = cartItem.ProductId,
                VariantId = cartItem.VariantId,
                Quantity = cartItem.Quantity,
                Message = "Item added to cart successfully"
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to cart. ProductId: {ProductId}, Quantity: {Quantity}",
                request.ProductId, request.Quantity);

            return Result.Failure<AddItemToCartResponseV1>(
                Error.Failure(
                    code: "Cart.AddItemFailed",
                    message: $"Failed to add item to cart: {ex.Message}"));
        }
    }
}
