using System.Net;
using System.Text.Json;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class RunEndpointIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RunEndpointIntegrationTests()
    {
        var stub = new StubBlobStorageService
        {
            RunsByProject =
            {
                ["project-alpha"] = new List<RunInfo>
                {
                    new("20260115-103000", new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero))
                },
                ["project-beta"] = new List<RunInfo>
                {
                    new("20260201-120000", new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero))
                }
            },
            LogsByRun =
            {
                ["project-alpha/20260115-103000"] = new LogListResponse(
                    "project-alpha",
                    "20260115-103000",
                    new List<LogItemInfo>
                    {
                        new("build.log", 1024, new DateTimeOffset(2026, 1, 15, 10, 31, 0, TimeSpan.Zero)),
                        new("deploy.log", 2048, new DateTimeOffset(2026, 1, 15, 10, 32, 0, TimeSpan.Zero))
                    },
                    new List<LogItemInfo>
                    {
                        new("system-prompt.txt", 512, new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero))
                    },
                    new List<LogItemInfo>
                    {
                        new("output.zip", 4096, new DateTimeOffset(2026, 1, 15, 10, 33, 0, TimeSpan.Zero))
                    }
                ),
                ["project-beta/20260201-120000"] = new LogListResponse(
                    "project-beta",
                    "20260201-120000",
                    new List<LogItemInfo>
                    {
                        new("main.log", 768, new DateTimeOffset(2026, 2, 1, 12, 1, 0, TimeSpan.Zero))
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
    }

    [Fact]
    public async Task ListRunLogs_ReturnsOkWithClassifiedLogs()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("logs", out var logs));
        Assert.True(doc.RootElement.TryGetProperty("prompts", out var prompts));
        Assert.True(doc.RootElement.TryGetProperty("artifacts", out var artifacts));
        Assert.Equal(2, logs.GetArrayLength());
        Assert.Equal(1, prompts.GetArrayLength());
        Assert.Equal(1, artifacts.GetArrayLength());
    }

    [Fact]
    public async Task ListRunLogs_Returns404WithProjectNotFound_WhenProjectDoesNotExist()
    {
        var response = await _client.GetAsync("/projects/nonexistent-project/runs/20260115-103000/logs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("Project not found", errorProp.GetString());
    }

    [Fact]
    public async Task ListRunLogs_Returns404WithRunNotFound_WhenRunDoesNotExist()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs/nonexistent-run/logs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("Run not found", errorProp.GetString());
    }

    [Fact]
    public async Task ListRunLogs_ResponseIncludesCorrectProjectIdAndRunId()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectId));
        Assert.Equal("project-alpha", projectId.GetString());
        Assert.True(doc.RootElement.TryGetProperty("run_id", out var runId));
        Assert.Equal("20260115-103000", runId.GetString());
    }

    [Fact]
    public async Task ListRunLogs_ResponseUsesSnakeCaseForLogItemProperties()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var firstLog = doc.RootElement.GetProperty("logs")[0];
        Assert.True(firstLog.TryGetProperty("name", out _));
        Assert.True(firstLog.TryGetProperty("size", out _));
        Assert.True(firstLog.TryGetProperty("last_modified", out _));
        Assert.False(firstLog.TryGetProperty("LastModified", out _));
    }

    [Fact]
    public async Task ListRunLogs_LogItemContainsCorrectSizeAndName()
    {
        var response = await _client.GetAsync("/projects/project-alpha/runs/20260115-103000/logs");
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var firstLog = doc.RootElement.GetProperty("logs")[0];
        Assert.Equal("build.log", firstLog.GetProperty("name").GetString());
        Assert.Equal(1024, firstLog.GetProperty("size").GetInt64());
    }

    [Fact]
    public async Task ListRunLogs_ReturnsOkWithOnlyLogs_WhenNoPromptsOrArtifacts()
    {
        var response = await _client.GetAsync("/projects/project-beta/runs/20260201-120000/logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(1, doc.RootElement.GetProperty("logs").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("prompts").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("artifacts").GetArrayLength());
    }

    [Fact]
    public async Task ListRunLogs_Returns500WithStorageError_WhenServiceThrows()
    {
        var throwingStub = new StubBlobStorageService
        {
            ExceptionToThrow = new Azure.RequestFailedException(503, "service unavailable")
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

        var response = await client.GetAsync("/projects/any-project/runs/any-run/logs");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("Storage account unavailable", errorProp.GetString());
    }
}
