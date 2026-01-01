using FastEndpoints;
using MediatR;
using Shopilent.API.Common.Models;
using Shopilent.Application.Features.Search.Queries.QuickSearch.V1;
using Shopilent.Domain.Common.Errors;

namespace Shopilent.API.Endpoints.Search.QuickSearch.V1;

public class QuickSearchEndpointV1 : Endpoint<QuickSearchRequestV1, ApiResponse<QuickSearchResponseV1>>
{
    private readonly ISender _sender;

    public QuickSearchEndpointV1(ISender sender)
    {
        _sender = sender;
    }

    public override void Configure()
    {
        Get("v1/search/quick");
        AllowAnonymous();
        Description(b => b
            .WithName("QuickSearch")
            .Produces<ApiResponse<QuickSearchResponseV1>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<QuickSearchResponseV1>>(StatusCodes.Status400BadRequest)
            .WithTags("Search"));
    }

    public override async Task HandleAsync(QuickSearchRequestV1 req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var errorResponse = ApiResponse<QuickSearchResponseV1>.Failure(
                ValidationFailures.Select(f => f.ErrorMessage).ToArray(),
                StatusCodes.Status400BadRequest);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        var query = new QuickSearchQueryV1(req.Query, req.Limit);

        var result = await _sender.Send(query, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            var errorResponse = ApiResponse<QuickSearchResponseV1>.Failure(
                result.Error.Message,
                statusCode);

            await SendAsync(errorResponse, errorResponse.StatusCode, ct);
            return;
        }

        var searchResult = result.Value;
        var response = new QuickSearchResponseV1
        {
            Suggestions = searchResult.Suggestions
                .Select(s => new ProductSearchSuggestionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Slug = s.Slug,
                    ImageUrl = s.ImageUrl,
                    ThumbnailUrl = s.ThumbnailUrl,
                    BasePrice = s.BasePrice
                })
                .ToArray(),
            TotalCount = searchResult.TotalCount,
            Query = searchResult.Query
        };

        var apiResponse = ApiResponse<QuickSearchResponseV1>.Success(
            response,
            "Quick search completed successfully");

        await SendAsync(apiResponse, StatusCodes.Status200OK, ct);
    }
}
