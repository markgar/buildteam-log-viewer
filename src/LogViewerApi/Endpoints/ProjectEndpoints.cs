using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class ProjectEndpoints
{
    public static WebApplication MapProjectEndpoints(this WebApplication app)
    {
        app.MapGet("/projects", async (IBlobStorageService service, CancellationToken ct) =>
        {
            var projects = await service.ListProjectsAsync(ct);
            return Results.Ok(new ProjectListResponse(projects));
        })
            .WithName("ListProjects")
            .WithOpenApi();

        app.MapGet("/projects/{projectId}/runs", async (string projectId, IBlobStorageService service, CancellationToken ct) =>
        {
            var runs = await service.ListRunsAsync(projectId, ct);
            if (runs is null)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }
            return Results.Ok(new RunListResponse(projectId, runs));
        })
            .WithName("ListRuns")
            .WithOpenApi();

        return app;
    }
}
