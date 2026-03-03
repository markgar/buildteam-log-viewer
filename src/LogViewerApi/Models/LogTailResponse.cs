namespace LogViewerApi.Models;

public record LogTailResponse(string ProjectId, string RunId, string Name, long TotalSize, int LinesReturned, string Content);
