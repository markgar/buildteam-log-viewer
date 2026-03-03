using LogViewerApi.Models;

namespace LogViewerApi.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .WithName("Health");

        return app;
    }
}
