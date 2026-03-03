using System.Net;
using System.Text.Json;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class OpenApiProjectIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiProjectIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiSpec_ContainsProjectsEndpointPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/projects", out _),
            "OpenAPI spec should contain /projects path");
    }

    [Fact]
    public async Task OpenApiSpec_ContainsProjectRunsEndpointPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/projects/{projectId}/runs", out _),
            "OpenAPI spec should contain /projects/{projectId}/runs path");
    }

    [Fact]
    public async Task OpenApiSpec_ProjectsEndpointHasListProjectsOperationId()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var projectsPath = doc.RootElement.GetProperty("paths").GetProperty("/projects");
        var getOp = projectsPath.GetProperty("get");
        Assert.True(getOp.TryGetProperty("operationId", out var opId));
        Assert.Equal("ListProjects", opId.GetString());
    }

    [Fact]
    public async Task OpenApiSpec_RunsEndpointHasListRunsOperationId()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var runsPath = doc.RootElement.GetProperty("paths").GetProperty("/projects/{projectId}/runs");
        var getOp = runsPath.GetProperty("get");
        Assert.True(getOp.TryGetProperty("operationId", out var opId));
        Assert.Equal("ListRuns", opId.GetString());
    }
}
