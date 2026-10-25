using System.Net;
using System.Text;
using System.Text.Json;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

public sealed class JsonContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Payload_UsesCamelCaseAndStringEnums()
    {
        var response = await _client.GetAsync("/test/json");

        Assert.Equal("""{"someValue":"Value","kind":"Medium"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_EnumAsString_Binds()
    {
        var response = await PostAsync("""{"someValue":"x","kind":"High"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("High", body.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Post_EnumAsNumber_Returns400()
    {
        var response = await PostAsync("""{"someValue":"x","kind":1}""");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_MissingRequiredField_Returns400WithCamelCaseKey()
    {
        var response = await PostAsync("""{"kind":"Low"}""");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(body.GetProperty("errors").TryGetProperty("someValue", out _));
    }

    private Task<HttpResponseMessage> PostAsync(string json) =>
        _client.PostAsync("/test/json", new StringContent(json, Encoding.UTF8, "application/json"));
}
