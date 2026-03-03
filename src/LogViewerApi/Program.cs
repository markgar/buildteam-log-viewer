using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Diagnostics;

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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        if (exception is RequestFailedException)
        {
            var message = $"Storage account unavailable: {exception.Message}";
            await context.Response.WriteAsJsonAsync(new { error = message });
            return;
        }

        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred" });
    });
});

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Log Viewer API");
});

app.Run();
