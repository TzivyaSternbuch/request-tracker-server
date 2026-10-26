using FluentValidation;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

public sealed class RequestQueryParametersValidator : AbstractValidator<RequestQueryParameters>
{
    public RequestQueryParametersValidator()
    {
        RuleFor(p => p.Page).InclusiveBetween(1, RequestQueryParameters.MaxPage);
        RuleFor(p => p.PageSize).InclusiveBetween(1, RequestQueryParameters.MaxPageSize);

        RuleFor(p => p.Status.Length).LessThanOrEqualTo(RequestQueryParameters.MaxFilterValues).OverridePropertyName("Status");
        RuleForEach(p => p.Status).IsInEnum();
        RuleFor(p => p.Priority.Length).LessThanOrEqualTo(RequestQueryParameters.MaxFilterValues).OverridePropertyName("Priority");
        RuleForEach(p => p.Priority).IsInEnum();
        RuleFor(p => p.AssignedTo.Length).LessThanOrEqualTo(RequestQueryParameters.MaxFilterValues).OverridePropertyName("AssignedTo");
        RuleForEach(p => p.AssignedTo).MaximumLength(Request.AssignedToMaxLength);

        // Longer than the column can never match, so it is a client error rather than an empty result.
        RuleFor(p => p.OrganizationName).MaximumLength(Request.OrganizationNameMaxLength);
        RuleFor(p => p.Search).MaximumLength(Math.Max(Request.TitleMaxLength, Request.OrganizationNameMaxLength));

        // The inclusive end date is queried as "before the next day", which doesn't exist for the last one.
        RuleFor(p => p.CreatedTo).LessThan(DateOnly.MaxValue);

        RuleFor(p => p.CreatedFrom)
            .LessThanOrEqualTo(p => p.CreatedTo)
            .When(p => p.CreatedFrom.HasValue && p.CreatedTo.HasValue)
            .WithMessage("'createdFrom' must be on or before 'createdTo'.");

        RuleForEach(p => p.Sort)
            .Must(value => string.IsNullOrWhiteSpace(value) || RequestSort.TryParse(value, out _))
            .WithMessage("'{PropertyValue}' is not a valid sort. Use a field name, optionally followed by ':asc' or ':desc'.");

        // A second key on the same field could never change the order.
        RuleFor(p => p.Sort)
            .Must(sort => sort
                .Select(value => RequestSort.TryParse(value, out var parsed) ? (RequestSortField?)parsed.Field : null)
                .OfType<RequestSortField>()
                .GroupBy(field => field)
                .All(group => group.Count() == 1))
            .WithMessage("Each sort field may appear only once.");
    }
}
