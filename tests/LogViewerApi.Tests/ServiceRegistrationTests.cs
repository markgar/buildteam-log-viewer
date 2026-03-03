using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

[Collection("EnvironmentTests")]
public class ServiceRegistrationTests
{
    [Fact]
    public void IBlobStorageService_IsResolvableFromDI()
    {
        var saved = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        try
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");

            using var factory = new WebApplicationFactory<Program>();
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<IBlobStorageService>();

            Assert.NotNull(service);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", saved);
        }
    }

    [Fact]
    public void IBlobStorageService_ResolvesToBlobStorageService()
    {
        var saved = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        try
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");

            using var factory = new WebApplicationFactory<Program>();
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<IBlobStorageService>();

            Assert.IsType<BlobStorageService>(service);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", saved);
        }
    }
}
