using LogViewerApi.Models;
using LogViewerApi.Services;

namespace LogViewerApi.Tests;

public class StubBlobStorageService : IBlobStorageService
{
    public List<ProjectInfo> Projects { get; set; } = new();
    public Dictionary<string, List<RunInfo>> RunsByProject { get; set; } = new();
    public Dictionary<string, LogListResponse> LogsByRun { get; set; } = new();
    public Dictionary<string, BlobContentResult> ContentByKey { get; set; } = new();
    public Dictionary<string, LogTailResponse> TailByKey { get; set; } = new();
    public Exception? ExceptionToThrow { get; set; }

    public Task CheckStorageHealthAsync(CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.CompletedTask;
    }

    public Task<List<ProjectInfo>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.FromResult(new List<ProjectInfo>(Projects));
    }

    public Task<List<RunInfo>?> ListRunsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        if (RunsByProject.TryGetValue(projectId, out var result))
            return Task.FromResult<List<RunInfo>?>(result);
        return Task.FromResult<List<RunInfo>?>(null);
    }

    public Task<bool> ProjectExistsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.FromResult(RunsByProject.ContainsKey(projectId));
    }

    public Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        var key = $"{projectId}/{runId}";
        if (LogsByRun.TryGetValue(key, out var result))
            return Task.FromResult<LogListResponse?>(result);
        return Task.FromResult<LogListResponse?>(null);
    }

    public Task<BlobContentResult?> GetLogContentAsync(string projectId, string runId, string fileName, long offset, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        var key = $"{projectId}/{runId}/{fileName}";
        if (ContentByKey.TryGetValue(key, out var result))
            return Task.FromResult<BlobContentResult?>(result);
        return Task.FromResult<BlobContentResult?>(null);
    }

    public Task<LogTailResponse?> GetLogTailAsync(string projectId, string runId, string fileName, int lines, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        var key = $"{projectId}/{runId}/{fileName}";
        if (TailByKey.TryGetValue(key, out var result))
            return Task.FromResult<LogTailResponse?>(result);
        return Task.FromResult<LogTailResponse?>(null);
    }
}
