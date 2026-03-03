namespace LogViewerApi.Models;

public record LogListResponse(string ProjectId, string RunId, IReadOnlyList<LogItemInfo> Logs, IReadOnlyList<LogItemInfo> Prompts, IReadOnlyList<LogItemInfo> Artifacts);
