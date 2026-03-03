using System.Net;
using System.Text.Json;
using Azure;
using Azure.Identity;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class ExceptionHandlerRunLogsIntegrationTests : IDisposable
{
    private readonly string? _savedStorageAccountUrl;

    public ExceptionHandlerRunLogsIntegrationTests()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl);
    }

    [Fact]
    public async Task GetRunLogs_Returns500WithGenericError_WhenNonAzureExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            ExceptionToThrow = new InvalidOperationException("unexpected failure")
        };

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IBlobStorageService>(stub);
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/projects/any-project/runs/any-run/logs");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("An unexpected error occurred", errorProp.GetString());
    }

    [Fact]
    public async Task GetRunLogs_Returns500WithStorageUnavailable_WhenAuthenticationFailedExceptionThrown()
    {
        var stub = new StubBlobStorageService
        {
            ExceptionToThrow = new AuthenticationFailedException("auth failed")
        };

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
                    if (descriptor is not null) services.Remove(descriptor);
                    services.AddSingleton<IBlobStorageService>(stub);
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

    [Fact]
    public async Task ErrorResponsesReturnJsonContentType_ForAllEndpoints()
    {
        var stub = new StubBlobStorageService
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
                    services.AddSingleton<IBlobStorageService>(stub);
                });
            });
        using var client = factory.CreateClient();

        var projectsResponse = await client.GetAsync("/projects");
        Assert.Contains("application/json", projectsResponse.Content.Headers.ContentType?.ToString());

        var runsResponse = await client.GetAsync("/projects/any-project/runs");
        Assert.Contains("application/json", runsResponse.Content.Headers.ContentType?.ToString());

        var logsResponse = await client.GetAsync("/projects/any-project/runs/any-run/logs");
        Assert.Contains("application/json", logsResponse.Content.Headers.ContentType?.ToString());
    }
}
