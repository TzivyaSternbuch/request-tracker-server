using FluentValidation.TestHelper;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Models;
using RequestTracker.Core.Validation;

namespace RequestTracker.UnitTests.Validation;

public sealed class UpdateStatusRequestValidatorTests
{
    private readonly UpdateStatusRequestValidator _validator = new();

    private static UpdateStatusRequest ValidRequest() => new()
    {
        Status = RequestStatus.InProgress,
        Version = new byte[UpdateStatusRequest.VersionLength],
        ChangedBy = "alice"
    };

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var request = ValidRequest();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(nameof(UpdateStatusRequest.Status))]
    [InlineData(nameof(UpdateStatusRequest.Version))]
    [InlineData(nameof(UpdateStatusRequest.ChangedBy))]
    public void Validate_MissingField_HasFieldError(string field)
    {
        var valid = ValidRequest();
        var request = field switch
        {
            nameof(UpdateStatusRequest.Status) => valid with { Status = null },
            nameof(UpdateStatusRequest.Version) => valid with { Version = null },
            _ => valid with { ChangedBy = null }
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(field);
    }

    [Fact]
    public void Validate_UndefinedStatus_HasStatusError()
    {
        var request = ValidRequest() with { Status = (RequestStatus)99 };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void Validate_VersionWrongLength_HasVersionError(int length)
    {
        var request = ValidRequest() with { Version = new byte[length] };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankChangedBy_HasChangedByError(string changedBy)
    {
        var request = ValidRequest() with { ChangedBy = changedBy };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.ChangedBy);
    }

    [Fact]
    public void Validate_ChangedByTooLong_HasChangedByError()
    {
        var request = ValidRequest() with { ChangedBy = new string('a', RequestStatusHistory.ChangedByMaxLength + 1) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.ChangedBy);
    }

    [Fact]
    public void Validate_ChangedByAtMaxLengthWithSurroundingSpaces_IsValid()
    {
        // The service stores the trimmed value, so the limit applies to it.
        var request = ValidRequest() with { ChangedBy = " " + new string('a', RequestStatusHistory.ChangedByMaxLength) + " " };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.ChangedBy);
    }
}
