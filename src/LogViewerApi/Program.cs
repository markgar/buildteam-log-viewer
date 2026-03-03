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
    var portEnv = Environment.GetEnvironmentVariable("PORT");
    var port = 8080;
    if (portEnv is not null)
    {
        if (!int.TryParse(portEnv, out port) || port < 1 || port > 65535)
        {
            throw new InvalidOperationException(
                $"PORT environment variable must be an integer between 1 and 65535, got: '{portEnv}'");
        }
    }
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
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

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

        if (exception is RequestFailedException or AuthenticationFailedException)
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
app.MapProjectEndpoints();
app.MapRunEndpoints();
app.MapLogEndpoints();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Log Viewer API");
});

app.Run();
