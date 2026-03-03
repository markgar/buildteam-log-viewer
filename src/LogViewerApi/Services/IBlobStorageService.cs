using LogViewerApi.Models;

namespace LogViewerApi.Services;

public interface IBlobStorageService
{
    Task<List<ProjectInfo>> ListProjectsAsync();
    Task<RunListResponse?> ListRunsAsync(string projectId);
}
