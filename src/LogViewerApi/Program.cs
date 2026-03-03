using Azure.Identity;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

var storageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL")
    ?? throw new InvalidOperationException("STORAGE_ACCOUNT_URL environment variable is required.");

builder.Services.AddSingleton(new BlobServiceClient(new Uri(storageAccountUrl), new DefaultAzureCredential()));

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

app.Run();
