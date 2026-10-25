using System.Net;
using System.Text;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

public sealed class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("notfound", HttpStatusCode.NotFound, TestController.NotFoundMessage)]
    [InlineData("conflict", HttpStatusCode.Conflict, TestController.ConflictMessage)]
    [InlineData("transition", HttpStatusCode.UnprocessableEntity, TestController.TransitionMessage)]
    public async Task Throw_DomainException_ReturnsMappedProblemDetails(
        string kind, HttpStatusCode expected, string message)
    {
        var response = await _client.GetAsync($"/test/throw/{kind}");

        var body = await ProblemAssert.IsProblemAsync(response, expected);
        Assert.Equal(message, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Throw_ValidationException_Returns400WithCamelCaseErrors()
    {
        var response = await _client.GetAsync("/test/throw/validation");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = body.GetProperty("errors");
        Assert.Equal(TestController.ValidationMessage, errors.GetProperty("status")[0].GetString());
        Assert.False(errors.TryGetProperty("Status", out _));
    }

    [Fact]
    public async Task Throw_NestedValidationException_CamelCasesEverySegment()
    {
        var response = await _client.GetAsync("/test/throw/validation-nested");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = body.GetProperty("errors");
        Assert.Equal(TestController.NestedValidationMessage, errors.GetProperty("items[0].version")[0].GetString());
    }

    [Fact]
    public async Task Throw_UnexpectedException_Returns500WithoutInternals()
    {
        var response = await _client.GetAsync("/test/throw/unexpected");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.InternalServerError);
        var raw = body.GetRawText();
        Assert.DoesNotContain(TestController.InternalMessage, raw);
        Assert.DoesNotContain(nameof(InvalidOperationException), raw);
        Assert.DoesNotContain("   at ", raw);
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task UnknownRoute_Get_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/nope");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WrongMethod_DeleteOnGetEndpoint_Returns405ProblemDetails()
    {
        var response = await _client.DeleteAsync("/test/json");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task MalformedJson_Post_Returns400ProblemDetails()
    {
        var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/test/json", content);

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(body.TryGetProperty("errors", out _));
    }
}
