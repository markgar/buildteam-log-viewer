using System.Net;
using System.Text.Json;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class OpenApiRunIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiRunIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiSpec_ContainsRunLogsEndpointPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/projects/{projectId}/runs/{runId}/logs", out _),
            "OpenAPI spec should contain /projects/{projectId}/runs/{runId}/logs path");
    }

    [Fact]
    public async Task OpenApiSpec_RunLogsEndpointHasListRunLogsOperationId()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var logsPath = doc.RootElement.GetProperty("paths")
            .GetProperty("/projects/{projectId}/runs/{runId}/logs");
        var getOp = logsPath.GetProperty("get");
        Assert.True(getOp.TryGetProperty("operationId", out var opId));
        Assert.Equal("ListRunLogs", opId.GetString());
    }
}
