using System.Net;
using System.Text.Json;
using Azure;
using Azure.Identity;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class LogContentCrossFeatureTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string? _savedStorageAccountUrl;

    public LogContentCrossFeatureTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        var lastModified = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var stub = new StubBlobStorageService
        {
            Projects = new List<ProjectInfo>
            {
                new("project-alpha", lastModified)
            },
            RunsByProject =
            {
                ["project-alpha"] = new List<RunInfo>
                {
                    new("20260301-100000", lastModified)
                }
            },
            LogsByRun =
            {
                ["project-alpha/20260301-100000"] = new LogListResponse(
                    "project-alpha",
                    "20260301-100000",
                    new List<LogItemInfo>
                    {
                        new("build.log", 2048, lastModified)
                    },
                    new List<LogItemInfo>(),
                    new List<LogItemInfo>())
            },
            ContentByKey =
            {
                ["project-alpha/20260301-100000/build.log"] = new BlobContentResult(
                    "build started\nbuild succeeded\n", 31, 31, lastModified, "text/plain")
            },
            TailByKey =
            {
                ["project-alpha/20260301-100000/build.log"] = new LogTailResponse(
                    "project-alpha", "20260301-100000", "build.log", 31, 2, "build started\nbuild succeeded")
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
    public async Task FullNavigationFlow_ProjectsToRunsToLogsToContent()
    {
        // List projects
        var projectsResponse = await _client.GetAsync("/projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        var projectsJson = await projectsResponse.Content.ReadAsStringAsync();
        using var projectsDoc = JsonDocument.Parse(projectsJson);
        var projectId = projectsDoc.RootElement.GetProperty("projects")[0].GetProperty("id").GetString();

        // List runs
        var runsResponse = await _client.GetAsync($"/projects/{projectId}/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runsJson = await runsResponse.Content.ReadAsStringAsync();
        using var runsDoc = JsonDocument.Parse(runsJson);
        var runId = runsDoc.RootElement.GetProperty("runs")[0].GetProperty("id").GetString();

        // List logs
        var logsResponse = await _client.GetAsync($"/projects/{projectId}/runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);
        var logsJson = await logsResponse.Content.ReadAsStringAsync();
        using var logsDoc = JsonDocument.Parse(logsJson);
        var logName = logsDoc.RootElement.GetProperty("logs")[0].GetProperty("name").GetString();

        // Get log content
        var contentResponse = await _client.GetAsync($"/projects/{projectId}/runs/{runId}/logs/{logName}");
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        var contentJson = await contentResponse.Content.ReadAsStringAsync();
        using var contentDoc = JsonDocument.Parse(contentJson);
        Assert.Equal(projectId, contentDoc.RootElement.GetProperty("project_id").GetString());
        Assert.Equal(runId, contentDoc.RootElement.GetProperty("run_id").GetString());
        Assert.Contains("build", contentDoc.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task FullNavigationFlow_ProjectsToRunsToLogsToTail()
    {
        // List projects → runs → logs → tail
        var projectsResponse = await _client.GetAsync("/projects");
        using var projectsDoc = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync());
        var projectId = projectsDoc.RootElement.GetProperty("projects")[0].GetProperty("id").GetString();

        var runsResponse = await _client.GetAsync($"/projects/{projectId}/runs");
        using var runsDoc = JsonDocument.Parse(await runsResponse.Content.ReadAsStringAsync());
        var runId = runsDoc.RootElement.GetProperty("runs")[0].GetProperty("id").GetString();

        var logsResponse = await _client.GetAsync($"/projects/{projectId}/runs/{runId}/logs");
        using var logsDoc = JsonDocument.Parse(await logsResponse.Content.ReadAsStringAsync());
        var logName = logsDoc.RootElement.GetProperty("logs")[0].GetProperty("name").GetString();

        var tailResponse = await _client.GetAsync($"/projects/{projectId}/runs/{runId}/logs/{logName}/tail");
        Assert.Equal(HttpStatusCode.OK, tailResponse.StatusCode);
        var tailJson = await tailResponse.Content.ReadAsStringAsync();
        using var tailDoc = JsonDocument.Parse(tailJson);
        Assert.Equal(projectId, tailDoc.RootElement.GetProperty("project_id").GetString());
        Assert.True(tailDoc.RootElement.GetProperty("lines_returned").GetInt32() > 0);
    }

    [Fact]
    public async Task ErrorEnvelopeConsistency_AcrossAllEndpointsIncludingLogContentAndTail()
    {
        // Project runs 404
        var runsResponse = await _client.GetAsync("/projects/nonexistent/runs");
        Assert.Equal(HttpStatusCode.NotFound, runsResponse.StatusCode);
        using var runsDoc = JsonDocument.Parse(await runsResponse.Content.ReadAsStringAsync());
        Assert.True(runsDoc.RootElement.TryGetProperty("error", out _));

        // Log content 404 - project not found
        var contentResponse = await _client.GetAsync("/projects/nonexistent/runs/any/logs/any.log");
        Assert.Equal(HttpStatusCode.NotFound, contentResponse.StatusCode);
        using var contentDoc = JsonDocument.Parse(await contentResponse.Content.ReadAsStringAsync());
        Assert.True(contentDoc.RootElement.TryGetProperty("error", out _));

        // Tail 404 - project not found
        var tailResponse = await _client.GetAsync("/projects/nonexistent/runs/any/logs/any.log/tail");
        Assert.Equal(HttpStatusCode.NotFound, tailResponse.StatusCode);
        using var tailDoc = JsonDocument.Parse(await tailResponse.Content.ReadAsStringAsync());
        Assert.True(tailDoc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ExceptionHandler_Returns500ForLogContentEndpoint_WhenServiceThrows()
    {
        var throwingStub = new StubBlobStorageService
        {
            ExceptionToThrow = new RequestFailedException(503, "service unavailable")
        };

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IBlobStorageService>(throwingStub);
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/projects/any/runs/any/logs/build.log");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("Storage account unavailable", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExceptionHandler_Returns500ForTailEndpoint_WhenServiceThrows()
    {
        var throwingStub = new StubBlobStorageService
        {
            ExceptionToThrow = new RequestFailedException(503, "service unavailable")
        };

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IBlobStorageService>(throwingStub);
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/projects/any/runs/any/logs/build.log/tail");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("Storage account unavailable", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OpenApi_IncludesLogContentAndTailEndpoints()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var pathKeys = new List<string>();
        foreach (var path in paths.EnumerateObject())
        {
            pathKeys.Add(path.Name);
        }

        Assert.Contains(pathKeys, p => p.Contains("logs") && p.Contains("tail"));
        Assert.Contains(pathKeys, p => p.Contains("logs") && !p.Contains("tail") && p.Contains("{fileName}"));
    }

    [Fact]
    public async Task LogContentAndTailEndpoints_ReturnJsonContentType()
    {
        var contentResponse = await _client.GetAsync(
            "/projects/project-alpha/runs/20260301-100000/logs/build.log");
        Assert.Contains("application/json", contentResponse.Content.Headers.ContentType?.ToString());

        var tailResponse = await _client.GetAsync(
            "/projects/project-alpha/runs/20260301-100000/logs/build.log/tail");
        Assert.Contains("application/json", tailResponse.Content.Headers.ContentType?.ToString());
    }
}
