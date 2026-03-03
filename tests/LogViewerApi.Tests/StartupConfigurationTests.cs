using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

public class StartupConfigurationTests
{
    [Fact]
    public void BlobServiceClient_IsRegisteredInDI()
    {
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");

        using var factory = new WebApplicationFactory<Program>();
        var blobClient = factory.Services.GetService<BlobServiceClient>();

        Assert.NotNull(blobClient);
    }

    [Fact]
    public void App_ThrowsOnStartup_WhenStorageAccountUrlMissing()
    {
        var savedUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        try
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", null);

            using var factory = new WebApplicationFactory<Program>();
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.True(
                ContainsExceptionType<InvalidOperationException>(ex),
                "Expected InvalidOperationException about missing STORAGE_ACCOUNT_URL");
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", savedUrl ?? "https://fake.blob.core.windows.net");
        }
    }

    private static bool ContainsExceptionType<T>(Exception ex) where T : Exception
    {
        if (ex is T) return true;
        if (ex is AggregateException agg)
        {
            return agg.InnerExceptions.Any(inner => ContainsExceptionType<T>(inner));
        }
        return ex.InnerException != null && ContainsExceptionType<T>(ex.InnerException);
    }
}
