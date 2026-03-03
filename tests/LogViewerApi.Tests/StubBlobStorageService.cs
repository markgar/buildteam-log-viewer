using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Tests;

public class StubBlobStorageService : IBlobStorageService
{
    public List<ProjectInfo> Projects { get; set; } = new();
    public Dictionary<string, RunListResponse> RunsByProject { get; set; } = new();
    public Exception? ExceptionToThrow { get; set; }

    public Task<List<ProjectInfo>> ListProjectsAsync()
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.FromResult(new List<ProjectInfo>(Projects));
    }

    public Task<RunListResponse?> ListRunsAsync(string projectId)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        if (RunsByProject.TryGetValue(projectId, out var result))
            return Task.FromResult<RunListResponse?>(result);
        return Task.FromResult<RunListResponse?>(null);
    }
}
