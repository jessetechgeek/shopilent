using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Features.Identity.Commands.Register.V1;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Identity.Register.V1;

public class RegisterEndpointV1 : Endpoint<RegisterRequestV1, ApiResponse<RegisterResponseV1>>
{
    private readonly IMediator _mediator;
    private readonly IAuthCookieService _authCookieService;

    public RegisterEndpointV1(IMediator mediator, IAuthCookieService authCookieService)
    {
        _mediator = mediator;
        _authCookieService = authCookieService;
    }

    public override void Configure()
    {
        Post("v1/auth/register");
        AllowAnonymous();
        Description(b => b
            .WithName("Register")
            .Produces<ApiResponse<RegisterResponseV1>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<RegisterResponseV1>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<RegisterResponseV1>>(StatusCodes.Status409Conflict)
            .WithTags("Identity"));
    }

    public override async Task HandleAsync(RegisterRequestV1 req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var errorResponse = ApiResponse<RegisterResponseV1>.Failure(
                ValidationFailures.Select(f => f.ErrorMessage).ToArray(),
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Map the request to command
        var command = new RegisterCommandV1
        {
            Email = req.Email,
            Password = req.Password,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone
        };

        // Send the command to the handler
        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            int statusCode;
            switch (result.Error?.Code)
            {
                case "User.EmailAlreadyExists":
                case "User.PhoneAlreadyExists":
                    statusCode = StatusCodes.Status409Conflict;
                    break;
                case var code when result.Error.Type == ErrorType.Validation:
                    statusCode = StatusCodes.Status400BadRequest;
                    break;
                case var code when result.Error.Type == ErrorType.Unauthorized:
                    statusCode = StatusCodes.Status401Unauthorized;
                    break;
                default:
                    statusCode = StatusCodes.Status500InternalServerError;
                    break;
            }

            var errorResponse = new ApiResponse<RegisterResponseV1>
            {
                Succeeded = false,
                Message = result.Error.Message,
                StatusCode = statusCode,
                Errors = new[] { result.Error.Message }
            };

            await SendAsync(errorResponse, statusCode, ct);
            return;
        }

        // Handle successful registration
        var registerResponse = result.Value;
        var isWebClient = _authCookieService.IsWebClient();

        // For web clients, set cookies and remove tokens from response body
        if (isWebClient)
        {
            _authCookieService.SetAuthCookies(registerResponse.AccessToken, registerResponse.RefreshToken);
        }

        var response = new ApiResponse<RegisterResponseV1>
        {
            Succeeded = true,
            Message = "Registration successful",
            StatusCode = StatusCodes.Status201Created,
            Data = new RegisterResponseV1
            {
                Id = registerResponse.User.Id,
                Email = registerResponse.User.Email.Value,
                FirstName = registerResponse.User.FullName.FirstName,
                LastName = registerResponse.User.FullName.LastName,
                EmailVerified = registerResponse.User.EmailVerified,
                Message = "Please check your email to verify your account",
                AccessToken = isWebClient ? string.Empty : registerResponse.AccessToken,
                RefreshToken = isWebClient ? string.Empty : registerResponse.RefreshToken
            }
        };

        await SendCreatedAtAsync("GetUserById", new { id = registerResponse.User.Id }, response, cancellation: ct);
    }
}
