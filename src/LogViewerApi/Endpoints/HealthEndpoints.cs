using Azure;
using Azure.Identity;
using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (IBlobStorageService service) =>
        {
            try
            {
                await service.CheckStorageHealthAsync();
                return Results.Ok(new HealthResponse("ok"));
            }
            catch (RequestFailedException)
            {
                return Results.Json(new ErrorResponse("Storage account unreachable"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (AuthenticationFailedException)
            {
                return Results.Json(new ErrorResponse("Storage account unreachable"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
            .WithName("Health")
            .WithOpenApi();

        return app;
    }
}
