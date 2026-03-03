using System.Net;
using System.Text.Json;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class LogContentValidationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly StubBlobStorageService _stub;
    private readonly string? _savedStorageAccountUrl;

    public LogContentValidationTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        var lastModified = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        _stub = new StubBlobStorageService
        {
            Projects = new List<ProjectInfo>
            {
                new("test-project", lastModified)
            },
            RunsByProject =
            {
                ["test-project"] = new List<RunInfo>
                {
                    new("20260301-100000", lastModified)
                }
            },
            ContentByKey =
            {
                ["test-project/20260301-100000/build.log"] = new BlobContentResult(
                    "line1\nline2\nline3\n", 18, 18, lastModified, "text/plain")
            },
            TailByKey =
            {
                ["test-project/20260301-100000/build.log"] = new LogTailResponse(
                    "test-project", "20260301-100000", "build.log", 18, 3, "line1\nline2\nline3")
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
                    services.AddSingleton<IBlobStorageService>(_stub);
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
    public async Task GetLogContent_NegativeOffset_Returns400BadRequest()
    {
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log?offset=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("non-negative", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetLogContent_NegativeOffset_ReturnsConsistentErrorEnvelope()
    {
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log?offset=-5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.False(string.IsNullOrEmpty(errorProp.GetString()));
    }

    [Fact]
    public async Task TailLog_ZeroLines_ClampedToMinimumOne()
    {
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log/tail?lines=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _stub.LastRequestedLines);
    }

    [Fact]
    public async Task TailLog_ExcessiveLines_ClampedToMaximum10000()
    {
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log/tail?lines=99999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10000, _stub.LastRequestedLines);
    }

    [Fact]
    public async Task TailLog_NegativeLines_ClampedToMinimumOne()
    {
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log/tail?lines=-5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _stub.LastRequestedLines);
    }

    [Fact]
    public async Task GetLogContent_RawModeWithOffsetAtFileSize_OmitsContentRangeHeader()
    {
        // offset=18 matches the file size (18 bytes), so Content-Range should NOT be set
        var response = await _client.GetAsync(
            "/projects/test-project/runs/20260301-100000/logs/build.log?raw=true&offset=18");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Null(response.Content.Headers.ContentRange);
    }

    [Fact]
    public async Task FullFlow_ListProjectsThenAccessLogWithNegativeOffset_Returns400()
    {
        // Navigate through the API hierarchy, then hit the negative offset validation
        var projectsResponse = await _client.GetAsync("/projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        using var projectsDoc = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync());
        var projectId = projectsDoc.RootElement.GetProperty("projects")[0].GetProperty("id").GetString();

        var runsResponse = await _client.GetAsync($"/projects/{projectId}/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        using var runsDoc = JsonDocument.Parse(await runsResponse.Content.ReadAsStringAsync());
        var runId = runsDoc.RootElement.GetProperty("runs")[0].GetProperty("id").GetString();

        // Negative offset should return 400
        var contentResponse = await _client.GetAsync(
            $"/projects/{projectId}/runs/{runId}/logs/build.log?offset=-10");
        Assert.Equal(HttpStatusCode.BadRequest, contentResponse.StatusCode);

        var content = await contentResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
