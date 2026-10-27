using FluentValidation;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

// Rules shared by the single and the bulk status update, so the two cannot drift apart.
internal static class StatusChangeRules
{
    public static IRuleBuilderOptions<T, RequestStatus?> ValidStatus<T>(this IRuleBuilder<T, RequestStatus?> rule) =>
        rule.NotNull().IsInEnum();

    public static IRuleBuilderOptions<T, byte[]?> ValidVersion<T>(this IRuleBuilderInitial<T, byte[]?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(v => v!.Length == UpdateStatusRequest.VersionLength)
            .WithMessage($"'Version' must be {UpdateStatusRequest.VersionLength} bytes.");

    // Checked on the trimmed value: that is what is stored in the audit row.
    public static IRuleBuilderOptions<T, string?> ValidChangedBy<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("'Changed By' must not be empty.")
            .Must(c => c!.Trim().Length <= RequestStatusHistory.ChangedByMaxLength)
            .WithMessage($"'Changed By' must be {RequestStatusHistory.ChangedByMaxLength} characters or fewer.");
}
