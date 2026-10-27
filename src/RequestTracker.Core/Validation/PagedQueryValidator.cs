using FluentValidation;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

// The paging limits shared by every paged endpoint.
public abstract class PagedQueryValidator<T> : AbstractValidator<T> where T : PagedQueryParameters
{
    protected PagedQueryValidator()
    {
        RuleFor(p => p.Page).InclusiveBetween(1, PagedQueryParameters.MaxPage);
        RuleFor(p => p.PageSize).InclusiveBetween(1, PagedQueryParameters.MaxPageSize);
    }
}
