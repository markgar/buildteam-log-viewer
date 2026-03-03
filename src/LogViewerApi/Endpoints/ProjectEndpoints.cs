using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class ProjectEndpoints
{
    public static WebApplication MapProjectEndpoints(this WebApplication app)
    {
        app.MapGet("/projects", async (IBlobStorageService service) =>
        {
            var projects = await service.ListProjectsAsync();
            return Results.Ok(new ProjectListResponse(projects));
        })
            .WithName("ListProjects")
            .WithOpenApi();

        app.MapGet("/projects/{projectId}/runs", async (string projectId, IBlobStorageService service) =>
        {
            var result = await service.ListRunsAsync(projectId);
            if (result is null)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }
            return Results.Ok(result);
        })
            .WithName("ListRuns")
            .WithOpenApi();

        return app;
    }
}
