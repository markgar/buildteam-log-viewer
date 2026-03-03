namespace LogViewerApi.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .WithOpenApi();

        return app;
    }
}
