using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Shipping.Errors;
using Shopilent.Domain.Shipping.Repositories.Write;

namespace Shopilent.Application.Features.Shipping.Commands.DeleteAddress.V1;

internal sealed class DeleteAddressCommandHandlerV1 : ICommandHandler<DeleteAddressCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;
    private readonly IAddressWriteRepository _addressWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeleteAddressCommandHandlerV1> _logger;

    public DeleteAddressCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IUserReadRepository userReadRepository,
        IAddressWriteRepository addressWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeleteAddressCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _userReadRepository = userReadRepository;
        _addressWriteRepository = addressWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteAddressCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get current user
            var currentUser =
                await _userReadRepository.GetByIdAsync(_currentUserContext.UserId!.Value, cancellationToken);
            if (currentUser == null)
            {
                return Result.Failure(UserErrors.NotFound(_currentUserContext.UserId.Value));
            }

            // Get address by ID
            var address = await _addressWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (address == null)
            {
                return Result.Failure(AddressErrors.NotFound(request.Id));
            }

            // Verify address belongs to current user
            if (address.UserId != _currentUserContext.UserId)
            {
                return Result.Failure(AddressErrors.NotFound(request.Id));
            }

            // Call the domain method to delete (this will raise AddressDeletedEvent)
            var deleteResult = address.Delete();
            if (deleteResult.IsFailure)
            {
                return deleteResult;
            }

            // Delete address from repository
            await _addressWriteRepository.DeleteAsync(address, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Address deleted successfully with ID: {AddressId} for user: {UserId}",
                address.Id, _currentUserContext.UserId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting address with ID {AddressId} for user {UserId}: {ErrorMessage}",
                request.Id, _currentUserContext.UserId, ex.Message);

            return Result.Failure(
                Error.Failure(
                    "Address.DeleteFailed",
                    "Failed to delete address"
                ));
        }
    }
}
