using System.Net;
using System.Text.Json;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class ProjectEndpointIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProjectEndpointIntegrationTests()
    {
        var stub = new StubBlobStorageService
        {
            Projects = new List<ProjectInfo>
            {
                new("project-alpha", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero)),
                new("project-beta", new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero))
            },
            RunsByProject =
            {
                ["project-alpha"] = new RunListResponse("project-alpha", new List<RunInfo>
                {
                    new("20260115-103000", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero)),
                    new("20260116-120000", new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero))
                })
            }
        };

        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IBlobStorageService>(stub);
                });
            });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetProjects_ReturnsOkWithProjectsArray()
    {
        var response = await _client.GetAsync("/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("projects", out var projects));
        Assert.Equal(JsonValueKind.Array, projects.ValueKind);
        Assert.Equal(2, projects.GetArrayLength());
    }

    [Fact]
    public async Task GetProjects_ResponseUsesSnakeCasePropertyNames()
    {
        var response = await _client.GetAsync("/projects");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var firstProject = doc.RootElement.GetProperty("projects")[0];
        Assert.True(firstProject.TryGetProperty("id", out _));
        Assert.True(firstProject.TryGetProperty("last_modified", out _));
        Assert.False(firstProject.TryGetProperty("LastModified", out _));
    }

    [Fact]
    public async Task GetProjectRuns_ReturnsOkWithRunsForExistingProject()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("runs", out var runs));
        Assert.Equal(JsonValueKind.Array, runs.ValueKind);
        Assert.Equal(2, runs.GetArrayLength());
    }

    [Fact]
    public async Task GetProjectRuns_Returns404ForNonExistentProject()
    {
        var response = await _client.GetAsync("/projects/nonexistent-project/runs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectRuns_404ResponseContainsProjectNotFoundMessage()
    {
        var response = await _client.GetAsync("/projects/nonexistent-project/runs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("Project not found", errorProp.GetString());
    }

    [Fact]
    public async Task GetProjectRuns_ResponseIncludesCorrectProjectId()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectId));
        Assert.Equal("project-alpha", projectId.GetString());
    }

    [Fact]
    public async Task GetProjectRuns_ResponseUsesSnakeCaseForRunProperties()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var firstRun = doc.RootElement.GetProperty("runs")[0];
        Assert.True(firstRun.TryGetProperty("id", out _));
        Assert.True(firstRun.TryGetProperty("last_modified", out _));
        Assert.False(firstRun.TryGetProperty("LastModified", out _));
    }
}
