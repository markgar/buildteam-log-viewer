using LogViewerApi.Models;

namespace LogViewerApi.Services;

public interface IBlobStorageService
{
    Task CheckStorageHealthAsync(CancellationToken cancellationToken = default);
    Task<List<ProjectInfo>> ListProjectsAsync(CancellationToken cancellationToken = default);
    Task<List<RunInfo>?> ListRunsAsync(string projectId, CancellationToken cancellationToken = default);
    Task<bool> ProjectExistsAsync(string projectId, CancellationToken cancellationToken = default);
    Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId, CancellationToken cancellationToken = default);
    Task<BlobContentResult?> GetLogContentAsync(string projectId, string runId, string fileName, long offset, CancellationToken cancellationToken = default);
}
