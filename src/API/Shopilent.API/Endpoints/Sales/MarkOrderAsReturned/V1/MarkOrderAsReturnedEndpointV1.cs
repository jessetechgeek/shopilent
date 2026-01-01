using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Common.Constants;
using Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Sales.MarkOrderAsReturned.V1;

public class MarkOrderAsReturnedEndpointV1 : Endpoint<MarkOrderAsReturnedRequestV1, ApiResponse<MarkOrderAsReturnedResponseV1>>
{
    private readonly IMediator _mediator;

    public MarkOrderAsReturnedEndpointV1(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("v1/orders/{orderId}/return");
        Description(b => b
            .WithName("MarkOrderAsReturned")
            .Produces<ApiResponse<MarkOrderAsReturnedResponseV1>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<MarkOrderAsReturnedResponseV1>>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<MarkOrderAsReturnedResponseV1>>(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<MarkOrderAsReturnedResponseV1>>(StatusCodes.Status403Forbidden)
            .Produces<ApiResponse<MarkOrderAsReturnedResponseV1>>(StatusCodes.Status404NotFound)
            .WithTags("Orders"));
        Policies(nameof(AuthorizationPolicy.RequireAuthenticated));
    }

    public override async Task HandleAsync(MarkOrderAsReturnedRequestV1 req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var errorResponse = ApiResponse<MarkOrderAsReturnedResponseV1>.Failure(
                ValidationFailures.Select(f => f.ErrorMessage).ToArray(),
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Get the order ID from the route
        var orderId = Route<Guid>("orderId");

        var command = new MarkOrderAsReturnedCommandV1
        {
            OrderId = orderId,
            ReturnReason = req.ReturnReason
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            var errorResponse = ApiResponse<MarkOrderAsReturnedResponseV1>.Failure(
                result.Error.Message,
                statusCode);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        var apiResponse = new MarkOrderAsReturnedResponseV1
        {
            OrderId = result.Value.OrderId,
            Status = result.Value.Status,
            ReturnReason = result.Value.ReturnReason,
            ReturnedAt = result.Value.ReturnedAt
        };

        var response = ApiResponse<MarkOrderAsReturnedResponseV1>.Success(
            apiResponse,
            "Order marked as returned successfully");

        await SendAsync(response, StatusCodes.Status200OK, ct);
    }
}
