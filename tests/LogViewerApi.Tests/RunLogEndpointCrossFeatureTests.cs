using System.Net;
using System.Text.Json;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class RunLogEndpointCrossFeatureTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string? _savedStorageAccountUrl;

    public RunLogEndpointCrossFeatureTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        var stub = new StubBlobStorageService
        {
            Projects = new List<ProjectInfo>
            {
                new("project-alpha", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero))
            },
            RunsByProject =
            {
                ["project-alpha"] = new List<RunInfo>
                {
                    new("20260115-103000", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero))
                }
            },
            LogsByRun =
            {
                ["project-alpha/20260115-103000"] = new LogListResponse(
                    "project-alpha",
                    "20260115-103000",
                    new List<LogItemInfo>
                    {
                        new("build.log", 1024, new DateTimeOffset(2026, 1, 15, 10, 31, 0, TimeSpan.Zero))
                    },
                    new List<LogItemInfo>(),
                    new List<LogItemInfo>()
                )
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
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl);
    }

    [Fact]
    public async Task ProjectListThenRunListThenLogList_FullNavigationFlow()
    {
        // List projects
        var projectsResponse = await _client.GetAsync("/projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        var projectsContent = await projectsResponse.Content.ReadAsStringAsync();
        using var projectsDoc = JsonDocument.Parse(projectsContent);
        var projectId = projectsDoc.RootElement.GetProperty("projects")[0].GetProperty("id").GetString();

        // List runs for the project
        var runsResponse = await _client.GetAsync($"/projects/{projectId}/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runsContent = await runsResponse.Content.ReadAsStringAsync();
        using var runsDoc = JsonDocument.Parse(runsContent);
        var runId = runsDoc.RootElement.GetProperty("runs")[0].GetProperty("id").GetString();

        // List logs for the run
        var logsResponse = await _client.GetAsync($"/projects/{projectId}/runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);
        var logsContent = await logsResponse.Content.ReadAsStringAsync();
        using var logsDoc = JsonDocument.Parse(logsContent);
        Assert.Equal(projectId, logsDoc.RootElement.GetProperty("project_id").GetString());
        Assert.Equal(runId, logsDoc.RootElement.GetProperty("run_id").GetString());
        Assert.True(logsDoc.RootElement.GetProperty("logs").GetArrayLength() > 0);
    }

    [Fact]
    public async Task HealthEndpointStillResponds_AlongsideRunLogEndpoints()
    {
        var healthResponse = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var logsResponse = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");
        Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);
    }

    [Fact]
    public async Task AllEndpointsReturnJsonContentType()
    {
        var projectsResponse = await _client.GetAsync("/projects");
        Assert.Contains("application/json", projectsResponse.Content.Headers.ContentType?.ToString());

        var runsResponse = await _client.GetAsync("/projects/project-alpha/runs");
        Assert.Contains("application/json", runsResponse.Content.Headers.ContentType?.ToString());

        var logsResponse = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");
        Assert.Contains("application/json", logsResponse.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task NotFoundErrorEnvelopeConsistent_AcrossProjectRunAndLogEndpoints()
    {
        // Project runs 404
        var runsResponse = await _client.GetAsync("/projects/nonexistent/runs");
        Assert.Equal(HttpStatusCode.NotFound, runsResponse.StatusCode);
        var runsContent = await runsResponse.Content.ReadAsStringAsync();
        using var runsDoc = JsonDocument.Parse(runsContent);
        Assert.True(runsDoc.RootElement.TryGetProperty("error", out _));

        // Run logs 404 - project not found
        var logsResponse = await _client.GetAsync("/projects/nonexistent/runs/any-run/logs");
        Assert.Equal(HttpStatusCode.NotFound, logsResponse.StatusCode);
        var logsContent = await logsResponse.Content.ReadAsStringAsync();
        using var logsDoc = JsonDocument.Parse(logsContent);
        Assert.True(logsDoc.RootElement.TryGetProperty("error", out _));

        // Run logs 404 - run not found
        var runNotFoundResponse = await _client.GetAsync("/projects/project-alpha/runs/nonexistent-run/logs");
        Assert.Equal(HttpStatusCode.NotFound, runNotFoundResponse.StatusCode);
        var runNotFoundContent = await runNotFoundResponse.Content.ReadAsStringAsync();
        using var runNotFoundDoc = JsonDocument.Parse(runNotFoundContent);
        Assert.True(runNotFoundDoc.RootElement.TryGetProperty("error", out _));
    }
}
