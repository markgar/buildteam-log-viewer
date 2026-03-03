using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using System.Text.Json;
using LogViewerApi.Endpoints;
using LogViewerApi.Models;
using LogViewerApi.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : 8080;
    options.ListenAnyIP(port);
});

var storageAccountUrl = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL")
    ?? throw new InvalidOperationException("STORAGE_ACCOUNT_URL environment variable is required.");

if (!Uri.TryCreate(storageAccountUrl, UriKind.Absolute, out var storageUri))
{
    throw new InvalidOperationException(
        $"STORAGE_ACCOUNT_URL is not a valid absolute URI: \"{storageAccountUrl}\"");
}

builder.Services.AddSingleton(new BlobServiceClient(storageUri, new DefaultAzureCredential()));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddOpenApi();

builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

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
            await context.Response.WriteAsJsonAsync(new ErrorResponse(message));
            return;
        }

        await context.Response.WriteAsJsonAsync(new ErrorResponse("An unexpected error occurred"));
    });
});

app.MapOpenApi();

app.MapHealthEndpoints();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Log Viewer API");
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

app.Run();
