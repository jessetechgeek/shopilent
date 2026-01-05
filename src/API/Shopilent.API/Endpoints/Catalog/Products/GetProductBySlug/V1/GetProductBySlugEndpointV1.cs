using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Features.Catalog.Queries.GetProductBySlug.V1;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Catalog.Products.GetProductBySlug.V1;

public class GetProductBySlugEndpointV1 : EndpointWithoutRequest<ApiResponse<ProductDetailDto>>
{
    private readonly IMediator _mediator;

    public GetProductBySlugEndpointV1(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("v1/products/slug/{slug}");
        AllowAnonymous();
        Description(b => b
            .WithName("GetProductBySlug")
            .Produces<ApiResponse<ProductDetailDto>>(StatusCodes.Status200OK, "application/json")
            .Produces<ApiResponse<ProductDetailDto>>(StatusCodes.Status404NotFound, "application/json")
            .WithTags("Products"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get the slug from the route
        var slug = Route<string>("slug");

        // Create query
        var query = new GetProductBySlugQueryV1 { Slug = slug };

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
