using MediatR;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Sales;
using Shopilent.Domain.Sales.Repositories.Write;

namespace Shopilent.Application.Features.Sales.Commands.CreateCart.V1;

internal sealed class CreateCartCommandHandlerV1 : IRequestHandler<CreateCartCommandV1, Result<CreateCartResponseV1>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartWriteRepository _cartWriteRepository;
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public CreateCartCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICartWriteRepository cartWriteRepository,
        IUserWriteRepository userWriteRepository,
        ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _cartWriteRepository = cartWriteRepository;
        _userWriteRepository = userWriteRepository;
        _currentUserContext = currentUserContext;
    }

    public async Task<Result<CreateCartResponseV1>> Handle(CreateCartCommandV1 request,
        CancellationToken cancellationToken)
    {
        // Get current user
        var userId = _currentUserContext.UserId;
        if (!userId.HasValue)
        {
            // Create anonymous cart
            var anonymousCartResult = Cart.Create();
            if (anonymousCartResult.IsFailure)
                return Result.Failure<CreateCartResponseV1>(anonymousCartResult.Error);

            var anonymousCart = anonymousCartResult.Value;

            // Add metadata if provided
            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    var metadataResult = anonymousCart.UpdateMetadata(item.Key, item.Value);
                    if (metadataResult.IsFailure)
                        return Result.Failure<CreateCartResponseV1>(metadataResult.Error);
                }
            }

            // Save cart
            var savedAnonymousCart = await _cartWriteRepository.AddAsync(anonymousCart, cancellationToken);

            // Return response
            return Result.Success(new CreateCartResponseV1
            {
                Id = savedAnonymousCart.Id,
                UserId = null,
                ItemCount = savedAnonymousCart.Items.Count,
                Metadata = savedAnonymousCart.Metadata,
                CreatedAt = savedAnonymousCart.CreatedAt
            });
        }

        // Get user for authenticated cart
        var user = await _userWriteRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return Result.Failure<CreateCartResponseV1>(
                UserErrors.NotFound(userId.Value));
        }

        // Check if user already has an active cart
        var existingCart = await _cartWriteRepository.GetByUserIdAsync(userId.Value, cancellationToken);
        if (existingCart != null)
        {
            // Return existing cart instead of creating a new one
            return Result.Success(new CreateCartResponseV1
            {
                Id = existingCart.Id,
                UserId = existingCart.UserId,
                ItemCount = existingCart.Items.Count,
                Metadata = existingCart.Metadata,
                CreatedAt = existingCart.CreatedAt
            });
        }

        // Create new cart for authenticated user
        Result<Cart> cartResult;
        if (request.Metadata != null)
        {
            cartResult = Cart.CreateWithMetadata(user, request.Metadata);
        }
        else
        {
            cartResult = Cart.Create(user);
        }

        if (cartResult.IsFailure)
            return Result.Failure<CreateCartResponseV1>(cartResult.Error);

        var cart = cartResult.Value;

        // Save cart
        var savedCart = await _cartWriteRepository.AddAsync(cart, cancellationToken);

        // Return response
        return Result.Success(new CreateCartResponseV1
        {
            Id = savedCart.Id,
            UserId = savedCart.UserId,
            ItemCount = savedCart.Items.Count,
            Metadata = savedCart.Metadata,
            CreatedAt = savedCart.CreatedAt
        });
    }
}
