using FluentValidation;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

public sealed class BulkUpdateStatusRequestValidator : AbstractValidator<BulkUpdateStatusRequest>
{
    public BulkUpdateStatusRequestValidator()
    {
        RuleFor(r => r.Status).ValidStatus();
        RuleFor(r => r.ChangedBy).ValidChangedBy();

        // The whole request is rejected on any bad item: a caller that sends a malformed batch has a bug, and
        // Partial Success is for per-item outcomes (stale, missing, not allowed), not for malformed input.
        RuleFor(r => r.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(items => items!.Count <= BulkUpdateStatusRequest.MaxItems)
            .WithMessage($"'Items' must contain at most {BulkUpdateStatusRequest.MaxItems} items.")
            .Must(items => !HasDuplicateIds(items!))
            .WithMessage("'Items' must not contain the same id twice.");

        RuleForEach(r => r.Items).NotNull().ChildRules(item =>
        {
            item.RuleFor(i => i.Id).NotNull();
            item.RuleFor(i => i.Version).ValidVersion();
        });
    }

    private static bool HasDuplicateIds(List<BulkStatusItem> items) =>
        items.Where(i => i?.Id is not null).GroupBy(i => i.Id).Any(g => g.Count() > 1);
}
