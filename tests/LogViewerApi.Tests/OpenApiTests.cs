using System.Net;
using System.Text.Json;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class OpenApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiEndpoint_Returns200_WithValidOpenApiJson()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("openapi", out var versionProp));
        Assert.StartsWith("3", versionProp.GetString());
    }

    [Fact]
    public async Task OpenApiEndpoint_ReturnsJsonContentType()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task OpenApiEndpoint_ContainsInfoAndPathsSections()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("info", out _));
        Assert.True(doc.RootElement.TryGetProperty("paths", out _));
    }

    [Fact]
    public async Task SwaggerUi_ReturnsHtmlContent()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.ToString();
        Assert.Contains("text/html", contentType);
    }
}
