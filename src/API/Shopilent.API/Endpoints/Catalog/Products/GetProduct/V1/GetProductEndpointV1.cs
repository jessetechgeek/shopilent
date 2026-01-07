using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Common.Constants;
using Shopilent.Application.Features.Catalog.Queries.GetProduct.V1;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Catalog.Products.GetProduct.V1;

public class GetProductEndpointV1 : EndpointWithoutRequest<ApiResponse<ProductDetailDto>>
{
    private readonly IMediator _mediator;

    public GetProductEndpointV1(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("v1/products/{id}");
        Description(b => b
            .WithName("GetProductById")
            .Produces<ApiResponse<ProductDetailDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<ProductDetailDto>>(StatusCodes.Status404NotFound)
            .WithTags("Products"));
        Policies(nameof(AuthorizationPolicy.RequireAdminOrManager));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get the ID from the route
        var id = Route<Guid>("id");

        // Create query
        var query = new GetProductQueryV1 { Id = id };

        // Send query to mediator
        var result = await _mediator.Send(query, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            var errorResponse = ApiResponse<ProductDetailDto>.Failure(
                result.Error.Message,
                statusCode);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        // Return successful response
        var response = ApiResponse<ProductDetailDto>.Success(
            result.Value,
            "Product retrieved successfully");

        await SendAsync(response, StatusCodes.Status200OK, ct);
    }
}
