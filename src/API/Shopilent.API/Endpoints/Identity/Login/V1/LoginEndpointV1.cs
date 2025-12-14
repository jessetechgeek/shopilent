using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Features.Identity.Commands.Login.V1;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Identity.Login.V1;

public class LoginEndpointV1 : Endpoint<LoginRequestV1, ApiResponse<LoginResponseV1>>
{
    private readonly IMediator _mediator;
    private readonly IAuthCookieService _authCookieService;

    public LoginEndpointV1(IMediator mediator, IAuthCookieService authCookieService)
    {
        _mediator = mediator;
        _authCookieService = authCookieService;
    }

    public override void Configure()
    {
        Post("v1/auth/login");
        AllowAnonymous();
        Description(b => b
            .WithName("Login")
            .Produces<ApiResponse<LoginResponseV1>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<LoginResponseV1>>(StatusCodes.Status401Unauthorized)
            .WithTags("Identity"));
    }

    public override async Task HandleAsync(LoginRequestV1 req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var errorResponse = ApiResponse<LoginResponseV1>.Failure(
                ValidationFailures.Select(f => f.ErrorMessage).ToArray(),
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Map the request to command
        var command = new LoginCommandV1
        {
            Email = req.Email,
            Password = req.Password,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        // Send the command to the handler
        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Type == ErrorType.Unauthorized
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;

            var errorResponse = new ApiResponse<LoginResponseV1>
            {
                Succeeded = false,
                Message = result.Error.Message,
                StatusCode = statusCode,
                Errors = new[] { result.Error.Message }
            };

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Handle successful login
        var loginResponse = result.Value;
        var isWebClient = _authCookieService.IsWebClient();

        // For web clients, set cookies and remove tokens from response body
        if (isWebClient)
        {
            _authCookieService.SetAuthCookies(loginResponse.AccessToken, loginResponse.RefreshToken);
        }

        var response = new ApiResponse<LoginResponseV1>
        {
            Succeeded = true,
            Message = "Login successful",
            StatusCode = StatusCodes.Status200OK,
            Data = new LoginResponseV1
            {
                Id = loginResponse.User.Id,
                Email = loginResponse.User.Email.Value,
                FirstName = loginResponse.User.FullName.FirstName,
                LastName = loginResponse.User.FullName.LastName,
                EmailVerified = loginResponse.User.EmailVerified,
                AccessToken = isWebClient ? string.Empty : loginResponse.AccessToken,
                RefreshToken = isWebClient ? string.Empty : loginResponse.RefreshToken
            }
        };

        await SendAsync(response, response.StatusCode, ct);
    }
}