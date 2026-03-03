using LogViewerApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogViewerApi.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void IBlobStorageService_IsResolvableFromDI()
    {
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");

        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetService<IBlobStorageService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void IBlobStorageService_ResolvesToBlobStorageService()
    {
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");

        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetService<IBlobStorageService>();

        Assert.IsType<BlobStorageService>(service);
    }

}
