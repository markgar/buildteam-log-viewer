using System.Net;
using System.Text.Json;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class HealthEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkStatusCode()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsJsonBodyWithStatusOk()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("status", out var statusProp));
        Assert.Equal("ok", statusProp.GetString());
    }

    [Fact]
    public async Task GetHealth_ReturnsJsonContentType()
    {
        var response = await _client.GetAsync("/health");
        var contentType = response.Content.Headers.ContentType?.ToString();

        Assert.NotNull(contentType);
        Assert.Contains("application/json", contentType);
    }

    [Fact]
    public async Task GetNonExistentEndpoint_Returns404()
    {
        var response = await _client.GetAsync("/nonexistent-path");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
