using FluentValidation;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

public sealed class StatusHistoryQueryParametersValidator : AbstractValidator<StatusHistoryQueryParameters>
{
    public StatusHistoryQueryParametersValidator()
    {
        RuleFor(p => p.Page).InclusiveBetween(1, RequestQueryParameters.MaxPage);
        RuleFor(p => p.PageSize).InclusiveBetween(1, RequestQueryParameters.MaxPageSize);
    }
}
