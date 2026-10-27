using FluentValidation;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Validation;

public sealed class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    public UpdateStatusRequestValidator()
    {
        RuleFor(r => r.Status).ValidStatus();
        RuleFor(r => r.Version).ValidVersion();
        RuleFor(r => r.ChangedBy).ValidChangedBy();
    }
}
