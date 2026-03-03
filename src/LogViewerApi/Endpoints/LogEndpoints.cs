using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Endpoints;

public static class LogEndpoints
{
    public static WebApplication MapLogEndpoints(this WebApplication app)
    {
        app.MapGet("/projects/{projectId}/runs/{runId}/logs/{fileName}/tail",
            async (string projectId, string runId, string fileName, int? lines, IBlobStorageService service, CancellationToken ct) =>
        {
            var lineCount = Math.Clamp(lines ?? 100, 1, 10000);
            var projectExists = await service.ProjectExistsAsync(projectId, ct);
            if (!projectExists)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }

            var result = await service.GetLogTailAsync(projectId, runId, fileName, lineCount, ct);
            if (result is null)
            {
                return Results.NotFound(new ErrorResponse("Log not found"));
            }

            return Results.Ok(result);
        })
            .Produces<LogTailResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .WithName("TailLog")
            .WithOpenApi();

        app.MapGet("/projects/{projectId}/runs/{runId}/logs/{**fileName}",
            async (string projectId, string runId, string fileName, bool? raw, long? offset, IBlobStorageService service, HttpContext context, CancellationToken ct) =>
        {
            var isRaw = raw ?? false;
            var byteOffset = offset ?? 0;
            if (byteOffset < 0)
            {
                return Results.BadRequest(new ErrorResponse("Offset must be non-negative."));
            }
            var projectExists = await service.ProjectExistsAsync(projectId, ct);
            if (!projectExists)
            {
                return Results.NotFound(new ErrorResponse("Project not found"));
            }

            var result = await service.GetLogContentAsync(projectId, runId, fileName, byteOffset, ct);
            if (result is null)
            {
                return Results.NotFound(new ErrorResponse("Log not found"));
            }

            if (isRaw)
            {
                if (byteOffset > 0 && byteOffset < result.Size)
                {
                    context.Response.Headers["Content-Range"] = $"bytes {byteOffset}-{result.Size - 1}/{result.Size}";
                }
                return Results.Text(result.Content, result.ContentType);
            }

            return Results.Ok(new LogContentResponse(
                projectId, runId, fileName, result.Size, result.Offset, result.LastModified, result.Content));
        })
            .Produces<LogContentResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .WithName("GetLogContent")
            .WithOpenApi();

        return app;
    }
}
