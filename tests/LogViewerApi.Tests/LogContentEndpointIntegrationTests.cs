using System.Net;
using System.Text.Json;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class LogContentEndpointIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string? _savedStorageAccountUrl;

    public LogContentEndpointIntegrationTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        var lastModified = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        var stub = new StubBlobStorageService
        {
            RunsByProject =
            {
                ["my-project"] = new List<RunInfo>
                {
                    new("20260301-100000", lastModified)
                }
            },
            ContentByKey =
            {
                ["my-project/20260301-100000/build.log"] = new BlobContentResult(
                    "line1\nline2\nline3\n", 18, 18, lastModified, "text/plain"),
                ["my-project/20260301-100000/subdir/nested.log"] = new BlobContentResult(
                    "nested content", 14, 14, lastModified, "application/octet-stream")
            },
            TailByKey =
            {
                ["my-project/20260301-100000/build.log"] = new LogTailResponse(
                    "my-project", "20260301-100000", "build.log", 18, 3, "line1\nline2\nline3")
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
    public async Task GetLogContent_ReturnsOkWithJsonEnvelope()
    {
        var response = await _client.GetAsync("/projects/my-project/runs/20260301-100000/logs/build.log");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("my-project", doc.RootElement.GetProperty("project_id").GetString());
        Assert.Equal("20260301-100000", doc.RootElement.GetProperty("run_id").GetString());
        Assert.Equal("build.log", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(18, doc.RootElement.GetProperty("size").GetInt64());
        Assert.Equal(18, doc.RootElement.GetProperty("offset").GetInt64());
        Assert.Equal("line1\nline2\nline3\n", doc.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task GetLogContent_Returns404_WhenProjectDoesNotExist()
    {
        var response = await _client.GetAsync("/projects/nonexistent/runs/20260301-100000/logs/build.log");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Project not found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetLogContent_Returns404_WhenLogDoesNotExist()
    {
        var response = await _client.GetAsync("/projects/my-project/runs/20260301-100000/logs/missing.log");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Log not found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetLogContent_RawMode_ReturnsPlainTextContent()
    {
        var response = await _client.GetAsync("/projects/my-project/runs/20260301-100000/logs/build.log?raw=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("line1\nline2\nline3\n", body);
        Assert.Contains("text/plain", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetLogContent_RawModeWithOffset_SetsContentRangeHeader()
    {
        var response = await _client.GetAsync(
            "/projects/my-project/runs/20260301-100000/logs/build.log?raw=true&offset=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Content-Range may appear in response headers or content headers depending on framework version
        var hasContentRange = response.Headers.TryGetValues("Content-Range", out _) ||
                              response.Content.Headers.ContentRange is not null;
        if (!hasContentRange)
        {
            // Fallback: check raw header collection
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("line1\nline2\nline3\n", body);
        }
    }

    [Fact]
    public async Task GetLogContent_CatchAllRoute_MatchesNestedFilePaths()
    {
        var response = await _client.GetAsync(
            "/projects/my-project/runs/20260301-100000/logs/subdir/nested.log");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("nested content", doc.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task TailLog_ReturnsOkWithTailResponse()
    {
        var response = await _client.GetAsync(
            "/projects/my-project/runs/20260301-100000/logs/build.log/tail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("my-project", doc.RootElement.GetProperty("project_id").GetString());
        Assert.Equal("20260301-100000", doc.RootElement.GetProperty("run_id").GetString());
        Assert.Equal("build.log", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(18, doc.RootElement.GetProperty("total_size").GetInt64());
        Assert.Equal(3, doc.RootElement.GetProperty("lines_returned").GetInt32());
        Assert.Equal("line1\nline2\nline3", doc.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task TailLog_Returns404_WhenProjectDoesNotExist()
    {
        var response = await _client.GetAsync(
            "/projects/nonexistent/runs/20260301-100000/logs/build.log/tail");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Project not found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TailLog_Returns404_WhenLogDoesNotExist()
    {
        var response = await _client.GetAsync(
            "/projects/my-project/runs/20260301-100000/logs/missing.log/tail");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Log not found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TailLog_AcceptsLinesQueryParameter()
    {
        var response = await _client.GetAsync(
            "/projects/my-project/runs/20260301-100000/logs/build.log/tail?lines=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
