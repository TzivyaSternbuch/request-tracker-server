using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ErrorHandlingTests(ApiFactory factory)
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
    public async Task Throw_ValidationExceptionWithoutErrors_Returns400WithMessage()
    {
        var response = await _client.GetAsync("/test/throw/validation-message");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(TestController.RequestLevelValidationMessage, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Throw_ValidationFailureWithoutPropertyName_Returns400UnderEmptyKey()
    {
        var response = await _client.GetAsync("/test/throw/validation-unnamed");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = body.GetProperty("errors");
        Assert.Equal(TestController.RequestLevelValidationMessage, errors.GetProperty("")[0].GetString());
    }

    [Fact]
    public async Task Throw_DomainExceptionWithHtmlAccept_StillReturnsProblemDetailsWithDetail()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/throw/conflict");
        request.Headers.Accept.ParseAdd("text/html");

        var response = await _client.SendAsync(request);

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(TestController.ConflictMessage, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AbortedRequest_DoesNotLogError()
    {
        var logs = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var client = host.CreateClient();

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync("/test/throw/aborted"));

        Assert.DoesNotContain(LogLevel.Error, logs.Levels);
    }

    [Fact]
    public async Task Throw_BadHttpRequestException_ReturnsItsStatusWithoutErrorLog()
    {
        var logs = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var client = host.CreateClient();

        var response = await client.GetAsync("/test/throw/badrequest");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.RequestEntityTooLarge);
        Assert.DoesNotContain(TestController.InternalMessage, body.GetRawText());
        Assert.DoesNotContain(LogLevel.Error, logs.Levels);
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
