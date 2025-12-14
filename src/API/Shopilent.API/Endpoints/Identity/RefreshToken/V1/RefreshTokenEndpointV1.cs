using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Features.Identity.Commands.RefreshToken.V1;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Identity.RefreshToken.V1;

public class RefreshTokenEndpointV1 : Endpoint<RefreshTokenRequestV1, ApiResponse<RefreshTokenResponseV1>>
{
    private readonly IMediator _mediator;
    private readonly IAuthCookieService _authCookieService;

    public RefreshTokenEndpointV1(IMediator mediator, IAuthCookieService authCookieService)
    {
        _mediator = mediator;
        _authCookieService = authCookieService;
    }

    public override void Configure()
    {
        Post("v1/auth/refresh-token");
        AllowAnonymous();
        Description(b => b
            .WithName("RefreshToken")
            .Produces<ApiResponse<RefreshTokenResponseV1>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<RefreshTokenResponseV1>>(StatusCodes.Status401Unauthorized)
            .WithTags("Identity"));
    }

    public override async Task HandleAsync(RefreshTokenRequestV1 req, CancellationToken ct)
    {
        var isWebClient = _authCookieService.IsWebClient();

        // For web clients, get refresh token from cookie with fallback to request body
        var refreshToken = isWebClient
            ? _authCookieService.GetRefreshTokenFromCookie() ?? req.RefreshToken
            : req.RefreshToken;

        // Validate that we have a refresh token
        if (string.IsNullOrEmpty(refreshToken))
        {
            var errorResponse = ApiResponse<RefreshTokenResponseV1>.Failure(
                new[] { "Refresh token is required" },
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        if (ValidationFailed)
        {
            var errorResponse = ApiResponse<RefreshTokenResponseV1>.Failure(
                ValidationFailures.Select(f => f.ErrorMessage).ToArray(),
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Map the request to command
        var command = new RefreshTokenCommandV1
        {
            RefreshToken = refreshToken,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        // Send the command to the handler
        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Type switch
            {
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.NotFound when result.Error.Code == "RefreshToken.NotFound" => StatusCodes.Status401Unauthorized,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

            var errorResponse = new ApiResponse<RefreshTokenResponseV1>
            {
                Succeeded = false,
                Message = result.Error.Message,
                StatusCode = statusCode,
                Errors = new[] { result.Error.Message }
            };

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Handle successful token refresh
        var tokenResponse = result.Value;

        // For web clients, set cookies and remove tokens from response body
        if (isWebClient)
        {
            _authCookieService.SetAuthCookies(tokenResponse.AccessToken, tokenResponse.RefreshToken);
        }

        var response = new ApiResponse<RefreshTokenResponseV1>
        {
            Succeeded = true,
            Message = "Token refresh successful",
            StatusCode = StatusCodes.Status200OK,
            Data = new RefreshTokenResponseV1
            {
                Id = tokenResponse.User.Id,
                Email = tokenResponse.User.Email.Value,
                FullName = $"{tokenResponse.User.FullName.FirstName} {tokenResponse.User.FullName.LastName}",
                AccessToken = isWebClient ? string.Empty : tokenResponse.AccessToken,
                RefreshToken = isWebClient ? string.Empty : tokenResponse.RefreshToken
            }
        };

        await SendAsync(response, response.StatusCode, ct);
    }
}