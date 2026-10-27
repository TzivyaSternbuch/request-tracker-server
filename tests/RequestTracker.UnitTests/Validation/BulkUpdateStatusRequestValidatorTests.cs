using FluentValidation.TestHelper;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Models;
using RequestTracker.Core.Validation;

namespace RequestTracker.UnitTests.Validation;

public sealed class BulkUpdateStatusRequestValidatorTests
{
    private readonly BulkUpdateStatusRequestValidator _validator = new();

    private static BulkStatusItem Item(int id) =>
        new() { Id = id, Version = new byte[UpdateStatusRequest.VersionLength] };

    private static BulkUpdateStatusRequest ValidRequest() => new()
    {
        Items = [Item(1), Item(2)],
        Status = RequestStatus.InProgress,
        ChangedBy = "alice",
    };

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(BulkUpdateStatusRequest.MaxItems)]
    public void Validate_ItemCountWithinLimits_IsValid(int count)
    {
        var request = ValidRequest() with { Items = Enumerable.Range(1, count).Select(Item).ToList() };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MoreThanMaxItems_HasItemsError()
    {
        var request = ValidRequest() with
        {
            Items = Enumerable.Range(1, BulkUpdateStatusRequest.MaxItems + 1).Select(Item).ToList()
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Validate_NoItems_HasItemsError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Items = [] });

        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Validate_ItemsMissing_HasItemsError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Items = null });

        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Validate_DuplicateId_HasItemsError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Items = [Item(1), Item(2), Item(1)] });

        result.ShouldHaveValidationErrorFor(r => r.Items);
    }

    [Fact]
    public void Validate_ItemWithoutId_HasItemError()
    {
        var request = ValidRequest() with { Items = [new BulkStatusItem { Version = new byte[8] }] };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Items[0].Id");
    }

    [Fact]
    public void Validate_ItemWithoutVersion_HasItemError()
    {
        var request = ValidRequest() with { Items = [new BulkStatusItem { Id = 1 }] };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Items[0].Version");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void Validate_ItemVersionWrongLength_HasItemError(int length)
    {
        var request = ValidRequest() with { Items = [new BulkStatusItem { Id = 1, Version = new byte[length] }] };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Items[0].Version");
    }

    [Fact]
    public void Validate_NullItem_IsInvalid()
    {
        var request = ValidRequest() with { Items = [Item(1), null!] };

        var result = _validator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MissingStatus_HasStatusError()
    {
        var result = _validator.TestValidate(ValidRequest() with { Status = null });

        result.ShouldHaveValidationErrorFor(r => r.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_BlankChangedBy_HasChangedByError(string? changedBy)
    {
        var result = _validator.TestValidate(ValidRequest() with { ChangedBy = changedBy });

        result.ShouldHaveValidationErrorFor(r => r.ChangedBy);
    }
}
