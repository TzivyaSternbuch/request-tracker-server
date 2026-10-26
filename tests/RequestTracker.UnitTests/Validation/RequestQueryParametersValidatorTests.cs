using FluentValidation.TestHelper;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Models;
using RequestTracker.Core.Validation;

namespace RequestTracker.UnitTests.Validation;

public sealed class RequestQueryParametersValidatorTests
{
    private readonly RequestQueryParametersValidator _validator = new();

    [Fact]
    public void Validate_Defaults_IsValid()
    {
        var parameters = new RequestQueryParameters();

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_PageBelowOne_HasPageError()
    {
        var parameters = new RequestQueryParameters { Page = 0 };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.Page);
    }

    [Fact]
    public void Validate_PageAboveMaximum_HasPageError()
    {
        var parameters = new RequestQueryParameters { Page = RequestQueryParameters.MaxPage + 1 };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.Page);
    }

    [Fact]
    public void Validate_PageAtMaximumWithMaxPageSize_IsValid()
    {
        var parameters = new RequestQueryParameters
        {
            Page = RequestQueryParameters.MaxPage,
            PageSize = RequestQueryParameters.MaxPageSize,
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RequestQueryParameters.MaxPageSize + 1)]
    public void Validate_PageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        var parameters = new RequestQueryParameters { PageSize = pageSize };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.PageSize);
    }

    [Fact]
    public void Validate_PageSizeAtMaximum_IsValid()
    {
        var parameters = new RequestQueryParameters { PageSize = RequestQueryParameters.MaxPageSize };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_CreatedFromAfterCreatedTo_HasCreatedFromError()
    {
        var parameters = new RequestQueryParameters
        {
            CreatedFrom = new DateOnly(2026, 2, 1),
            CreatedTo = new DateOnly(2026, 1, 31),
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.CreatedFrom);
    }

    [Fact]
    public void Validate_CreatedFromEqualsCreatedTo_IsValid()
    {
        var parameters = new RequestQueryParameters
        {
            CreatedFrom = new DateOnly(2026, 1, 31),
            CreatedTo = new DateOnly(2026, 1, 31),
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_CreatedToLastRepresentableDay_HasCreatedToError()
    {
        var parameters = new RequestQueryParameters { CreatedTo = DateOnly.MaxValue };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.CreatedTo);
    }

    [Fact]
    public void Validate_SeveralValuesPerSelectionFilter_IsValid()
    {
        var parameters = new RequestQueryParameters
        {
            Status = [RequestStatus.New, RequestStatus.InProgress],
            Priority = [RequestPriority.Low, RequestPriority.High],
            AssignedTo = ["Noa Levi", "Dan Cohen"],
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UndefinedStatus_HasStatusError()
    {
        var parameters = new RequestQueryParameters { Status = [RequestStatus.New, (RequestStatus)99] };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor("Status[1]");
    }

    [Fact]
    public void Validate_UndefinedPriority_HasPriorityError()
    {
        var parameters = new RequestQueryParameters { Priority = [(RequestPriority)99] };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor("Priority[0]");
    }

    [Fact]
    public void Validate_AssignedToOverColumnLength_HasAssignedToError()
    {
        var parameters = new RequestQueryParameters { AssignedTo = [new string('x', Request.AssignedToMaxLength + 1)] };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor("AssignedTo[0]");
    }

    [Fact]
    public void Validate_TooManyAssignees_HasAssignedToError()
    {
        var parameters = new RequestQueryParameters
        {
            AssignedTo = Enumerable.Range(0, RequestQueryParameters.MaxFilterValues + 1).Select(i => $"user{i}").ToArray(),
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.AssignedTo);
    }

    [Fact]
    public void Validate_OrganizationNameOverColumnLength_HasOrganizationNameError()
    {
        var parameters = new RequestQueryParameters
        {
            OrganizationName = new string('x', Request.OrganizationNameMaxLength + 1),
        };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.OrganizationName);
    }

    [Fact]
    public void Validate_SearchOverColumnLength_HasSearchError()
    {
        var parameters = new RequestQueryParameters { Search = new string('x', Request.TitleMaxLength + 1) };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.Search);
    }

    [Theory]
    [InlineData("priority:desc", "createdAt:asc")]
    [InlineData("Title", "ID:DESC")]
    [InlineData("organizationName", "status", "assignedTo", "updatedAt")]
    public void Validate_ValidSortFields_IsValid(params string[] sort)
    {
        var parameters = new RequestQueryParameters { Sort = sort };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_BlankSortValues_IsValid()
    {
        var parameters = new RequestQueryParameters { Sort = [null!, "", " "] };

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ParseSort_OnlyBlankValues_ReturnsDefault()
    {
        var parameters = new RequestQueryParameters { Sort = ["", " "] };

        var sorts = parameters.ParseSort();

        Assert.Equal(new[] { RequestSort.Default }, sorts);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("title:sideways")]
    [InlineData("2")]
    [InlineData("title:asc:desc")]
    [InlineData(":desc")]
    public void Validate_InvalidSortValue_HasSortItemError(string value)
    {
        var parameters = new RequestQueryParameters { Sort = ["createdAt", value] };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor("Sort[1]");
    }

    [Fact]
    public void Validate_SameSortFieldTwice_HasSortError()
    {
        var parameters = new RequestQueryParameters { Sort = ["title:asc", "Title:desc"] };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.Sort);
    }
}
