using FastEndpoints;
using FluentValidation;

namespace Shopilent.API.Endpoints.Search.QuickSearch.V1;

public class QuickSearchRequestValidatorV1 : Validator<QuickSearchRequestV1>
{
    public QuickSearchRequestValidatorV1()
    {
        RuleFor(x => x.Query)
            .MinimumLength(3).WithMessage("Search query must be at least 3 characters long.")
            .MaximumLength(200).WithMessage("Search query must not exceed 200 characters.");

        RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("Limit must be greater than 0.")
            .LessThanOrEqualTo(20).WithMessage("Limit must not exceed 20 for quick search.");
    }
}
