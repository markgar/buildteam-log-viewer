using LogViewerApi.Models;

namespace LogViewerApi.Services;

public interface IBlobStorageService
{
    Task<List<ProjectInfo>> ListProjectsAsync();
    Task<RunListResponse?> ListRunsAsync(string projectId);
    Task<bool> ProjectExistsAsync(string projectId);
    Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId);
}
