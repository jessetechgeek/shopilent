using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.DTOs;
using Shopilent.Domain.Identity.Errors;
using Shopilent.Domain.Identity.Repositories.Read;

namespace Shopilent.Application.Features.Identity.Queries.GetUser.V1;

internal sealed class GetUserQueryHandlerV1 : IQueryHandler<GetUserQueryV1, UserDetailDto>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly ILogger<GetUserQueryHandlerV1> _logger;

    public GetUserQueryHandlerV1(
        IUserReadRepository userReadRepository,
        ILogger<GetUserQueryHandlerV1> logger)
    {
        _userReadRepository = userReadRepository;
        _logger = logger;
    }

    public async Task<Result<UserDetailDto>> Handle(
        GetUserQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userReadRepository.GetDetailByIdAsync(request.Id, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} was not found", request.Id);
                return Result.Failure<UserDetailDto>(UserErrors.NotFound(request.Id));
            }

            _logger.LogInformation("Retrieved user detail with ID {UserId}", request.Id);
            return Result.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user detail with ID {UserId}", request.Id);

            return Result.Failure<UserDetailDto>(
                Error.Failure(
                    code: "User.GetDetailFailed",
                    message: $"Failed to retrieve user detail: {ex.Message}"));
        }
    }
}
