using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class LogEndpoints
{
    public static WebApplication MapLogEndpoints(this WebApplication app)
    {
        app.MapGet("/projects/{projectId}/runs/{runId}/logs/{**fileName}",
            async (string projectId, string runId, string fileName, bool raw, long offset, IBlobStorageService service, CancellationToken ct) =>
        {
            var projectExists = await service.ProjectExistsAsync(projectId, ct);
            if (!projectExists)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }

            var result = await service.GetLogContentAsync(projectId, runId, fileName, offset, ct);
            if (result is null)
            {
                return Results.NotFound(new ErrorResponse("Log not found"));
            }

            if (raw)
            {
                return Results.Text(result.Content, result.ContentType);
            }

            return Results.Ok(new LogContentResponse(
                projectId, runId, fileName, result.Size, result.Offset, result.LastModified, result.Content));
        })
            .WithName("GetLogContent")
            .WithOpenApi();

        return app;
    }
}
