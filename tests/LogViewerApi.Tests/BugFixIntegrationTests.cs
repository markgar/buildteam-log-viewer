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
public class BugFixIntegrationTests : IDisposable
{
    private readonly string? _savedStorageAccountUrl;

    public BugFixIntegrationTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl);
    }

    private static StubBlobStorageService CreateStubWithData()
    {
        var lastModified = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        return new StubBlobStorageService
        {
            Projects = new List<ProjectInfo> { new("proj1", lastModified) },
            RunsByProject =
            {
                ["proj1"] = new List<RunInfo> { new("20260301-100000", lastModified) }
            },
            ContentByKey =
            {
                ["proj1/20260301-100000/build.log"] = new BlobContentResult(
                    "line1\nline2\nline3\n", 18, 18, lastModified, "text/plain")
            },
            TailByKey =
            {
                ["proj1/20260301-100000/build.log"] = new LogTailResponse(
                    "proj1", "20260301-100000", "build.log", 18, 3, "line1\nline2\nline3")
            }
        };
    }

    private WebApplicationFactory<Program> CreateFactory(IBlobStorageService stub)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton(stub);
                });
            });
    }

    // --- Priority 1: Health endpoint consolidated catch block ---

    [Fact]
    public async Task HealthEndpoint_Returns503_WhenAuthenticationFailedExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            ExceptionToThrow = new AuthenticationFailedException("credential unavailable")
        };

        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("unreachable", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_Returns503_WhenRequestFailedExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            ExceptionToThrow = new RequestFailedException(503, "service unavailable")
        };

        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("unreachable", doc.RootElement.GetProperty("error").GetString());
    }

    // --- Priority 1: Content-Range header logic ---

    [Fact]
    public async Task RawModeWithValidOffset_SetsContentRangeHeader()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log?raw=true&offset=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(response.Content.Headers.ContentRange);
    }

    [Fact]
    public async Task RawModeWithZeroOffset_OmitsContentRangeHeader()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log?raw=true&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentRange);
    }

    // --- Priority 1: Offset tracking through the stub ---

    [Fact]
    public async Task LogContentEndpoint_PassesOffsetToService()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log?offset=7");

        Assert.Equal(7, stub.LastRequestedOffset);
    }

    [Fact]
    public async Task LogContentEndpoint_PassesZeroOffsetByDefault()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        await client.GetAsync("/projects/proj1/runs/20260301-100000/logs/build.log");

        Assert.Equal(0, stub.LastRequestedOffset);
    }

    // --- Priority 1: Tail default lines parameter ---

    [Fact]
    public async Task TailEndpoint_DefaultsTo100Lines()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        await client.GetAsync("/projects/proj1/runs/20260301-100000/logs/build.log/tail");

        Assert.Equal(100, stub.LastRequestedLines);
    }

    // --- Priority 2: Integration - full flow with offset tracking ---

    [Fact]
    public async Task FullFlow_ListProjectsThenGetLogWithOffset_VerifiesOffsetPassedThrough()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        // Step 1: List projects
        var projectsResponse = await client.GetAsync("/projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        using var projectsDoc = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync());
        var projectId = projectsDoc.RootElement.GetProperty("projects")[0].GetProperty("id").GetString();

        // Step 2: List runs
        var runsResponse = await client.GetAsync($"/projects/{projectId}/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        using var runsDoc = JsonDocument.Parse(await runsResponse.Content.ReadAsStringAsync());
        var runId = runsDoc.RootElement.GetProperty("runs")[0].GetProperty("id").GetString();

        // Step 3: Get log content with specific offset
        var contentResponse = await client.GetAsync(
            $"/projects/{projectId}/runs/{runId}/logs/build.log?offset=10");
        Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);

        // Verify offset was passed through to the service
        Assert.Equal(10, stub.LastRequestedOffset);
    }

    [Fact]
    public async Task FullFlow_ListProjectsThenTailWithClampedLines_VerifiesClamping()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        // Navigate to tail endpoint with excessive lines
        var projectsResponse = await client.GetAsync("/projects");
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);

        var runsResponse = await client.GetAsync("/projects/proj1/runs");
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);

        var tailResponse = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log/tail?lines=50000");
        Assert.Equal(HttpStatusCode.OK, tailResponse.StatusCode);

        // Verify the service received the clamped value (10000, not 50000)
        Assert.Equal(10000, stub.LastRequestedLines);
    }

    // --- Priority 2: Exception handler integration for auth failures on content/tail ---

    [Fact]
    public async Task ExceptionHandler_LogContent_Returns500_WhenAuthenticationFailedExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            RunsByProject = { ["proj1"] = new List<RunInfo>() },
            ExceptionToThrow = new AuthenticationFailedException("auth failed")
        };

        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("Storage account unavailable", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExceptionHandler_TailEndpoint_Returns500_WhenAuthenticationFailedExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            RunsByProject = { ["proj1"] = new List<RunInfo>() },
            ExceptionToThrow = new AuthenticationFailedException("auth failed")
        };

        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log/tail");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Contains("Storage account unavailable", doc.RootElement.GetProperty("error").GetString());
    }

    // --- Priority 2: Health vs. validation error independence ---

    [Fact]
    public async Task NegativeOffsetReturns400_EvenWhenHealthEndpointWorks()
    {
        var stub = CreateStubWithData();
        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        // Health endpoint returns 200
        var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        // Negative offset still returns 400 (validation, not storage error)
        var contentResponse = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log?offset=-1");
        Assert.Equal(HttpStatusCode.BadRequest, contentResponse.StatusCode);

        var body = await contentResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // --- Priority 2: Error envelope consistency across health and log validation ---

    [Fact]
    public async Task ErrorEnvelopeConsistency_HealthUnavailableAndLogValidation_BothUseErrorProperty()
    {
        var stub = CreateStubWithData();
        stub.ExceptionToThrow = new AuthenticationFailedException("auth failed");

        using var factory = CreateFactory(stub);
        using var client = factory.CreateClient();

        // Health endpoint: 503 with error envelope
        var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, healthResponse.StatusCode);
        var healthBody = await healthResponse.Content.ReadAsStringAsync();
        using var healthDoc = JsonDocument.Parse(healthBody);
        Assert.True(healthDoc.RootElement.TryGetProperty("error", out _),
            "Health 503 should use consistent error envelope");

        // Log content with negative offset: 400 with error envelope
        // (The negative offset check happens before the service call, so no exception)
        var logResponse = await client.GetAsync(
            "/projects/proj1/runs/20260301-100000/logs/build.log?offset=-1");
        Assert.Equal(HttpStatusCode.BadRequest, logResponse.StatusCode);
        var logBody = await logResponse.Content.ReadAsStringAsync();
        using var logDoc = JsonDocument.Parse(logBody);
        Assert.True(logDoc.RootElement.TryGetProperty("error", out _),
            "Log 400 should use consistent error envelope");
    }
}
