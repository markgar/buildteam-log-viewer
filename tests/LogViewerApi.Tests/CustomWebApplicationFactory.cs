using LogViewerApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LogViewerApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _savedStorageAccountUrl;

    public CustomWebApplicationFactory()
    {
        _savedStorageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL");
        Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", "https://fake.blob.core.windows.net");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddSingleton<IBlobStorageService>(new StubBlobStorageService());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl);
        }
    }
}
