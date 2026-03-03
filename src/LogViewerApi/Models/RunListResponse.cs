namespace LogViewerApi.Models;

public record RunListResponse(string ProjectId, IReadOnlyList<RunInfo> Runs);
