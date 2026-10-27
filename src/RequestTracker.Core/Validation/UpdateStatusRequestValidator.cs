using FluentValidation;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

public sealed class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    public UpdateStatusRequestValidator()
    {
        RuleFor(r => r.Status).NotNull().IsInEnum();

        RuleFor(r => r.Version).NotNull();
        RuleFor(r => r.Version!.Length).Equal(UpdateStatusRequest.VersionLength)
            .When(r => r.Version is not null)
            .OverridePropertyName(nameof(UpdateStatusRequest.Version));

        // Checked on the trimmed value: that is what is stored in the audit row.
        RuleFor(r => r.ChangedBy)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("'Changed By' must not be empty.");
        RuleFor(r => r.ChangedBy)
            .Must(c => c!.Trim().Length <= RequestStatusHistory.ChangedByMaxLength)
            .When(r => r.ChangedBy is not null)
            .WithMessage($"'Changed By' must be {RequestStatusHistory.ChangedByMaxLength} characters or fewer.");
    }
}
