namespace LogViewerApi.Models;

public record LogContentResponse(string ProjectId, string RunId, string Name, long Size, long Offset, DateTimeOffset LastModified, string Content);
