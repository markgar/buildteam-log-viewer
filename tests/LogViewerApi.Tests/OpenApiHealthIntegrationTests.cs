using System.Net;
using System.Text.Json;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class OpenApiHealthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiHealthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiSpec_ContainsHealthEndpointPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/health", out _),
            "OpenAPI spec should contain /health path after WithOpenApi() was added");
    }

    [Fact]
    public async Task OpenApiSpec_HealthEndpointHasGetOperation()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var healthPath = doc.RootElement.GetProperty("paths").GetProperty("/health");
        Assert.True(healthPath.TryGetProperty("get", out var getOp),
            "Health endpoint should have a GET operation in the OpenAPI spec");

        Assert.True(getOp.TryGetProperty("operationId", out var opId));
        Assert.Equal("Health", opId.GetString());
    }
}
