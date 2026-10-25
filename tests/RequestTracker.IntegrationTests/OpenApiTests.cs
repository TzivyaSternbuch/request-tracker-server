using System.Net;
using System.Text.Json;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OpenApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_OpenApiDocument_Returns200()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_OpenApiDocument_DescribesEnumsAsStrings()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var kind = document.GetProperty("components").GetProperty("schemas").GetProperty(nameof(TestKind));
        var values = kind.GetProperty("enum").EnumerateArray().Select(v => v.GetString()!).ToArray();
        Assert.Equal(new[] { "Low", "Medium", "High" }, values);
    }

    [Fact]
    public async Task Get_SwaggerUi_Returns200()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
