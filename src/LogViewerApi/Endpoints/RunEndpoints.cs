using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class RunEndpoints
{
    public static WebApplication MapRunEndpoints(this WebApplication app)
    {
        app.MapGet("/projects/{projectId}/runs/{runId}/logs", async (string projectId, string runId, IBlobStorageService service, CancellationToken ct) =>
        {
            var projectExists = await service.ProjectExistsAsync(projectId, ct);
            if (!projectExists)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }

            var result = await service.ListRunLogsAsync(projectId, runId, ct);
            if (result is null)
            {
                return Results.NotFound(new ErrorResponse("Run not found"));
            }

            return Results.Ok(result);
        })
            .Produces<LogListResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .WithName("ListRunLogs")
            .WithOpenApi();

        return app;
    }
}
