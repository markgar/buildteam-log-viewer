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

        return app;
    }
}
