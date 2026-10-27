using FluentValidation.TestHelper;
using RequestTracker.Core.Models;
using RequestTracker.Core.Validation;

namespace RequestTracker.UnitTests.Validation;

public sealed class StatusHistoryQueryParametersValidatorTests
{
    private readonly StatusHistoryQueryParametersValidator _validator = new();

    [Fact]
    public void Validate_Defaults_IsValid()
    {
        var parameters = new StatusHistoryQueryParameters();

        var result = _validator.TestValidate(parameters);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RequestQueryParameters.MaxPage + 1)]
    public void Validate_PageOutOfRange_HasPageError(int page)
    {
        var parameters = new StatusHistoryQueryParameters { Page = page };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RequestQueryParameters.MaxPageSize + 1)]
    public void Validate_PageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        var parameters = new StatusHistoryQueryParameters { PageSize = pageSize };

        var result = _validator.TestValidate(parameters);

        result.ShouldHaveValidationErrorFor(p => p.PageSize);
    }
}
