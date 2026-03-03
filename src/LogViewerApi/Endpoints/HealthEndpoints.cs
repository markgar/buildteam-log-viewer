using Azure;
using Azure.Identity;
using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (IBlobStorageService service, CancellationToken ct) =>
        {
            try
            {
                await service.CheckStorageHealthAsync(ct);
                return Results.Ok(new HealthResponse("ok"));
            }
            catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException)
            {
                return Results.Json(new ErrorResponse("Storage account unreachable"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .WithName("Health")
            .WithOpenApi();

        return app;
    }
}
