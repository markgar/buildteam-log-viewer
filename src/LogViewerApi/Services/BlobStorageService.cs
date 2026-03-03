using Azure;
using Azure.Storage.Blobs;
using LogViewerApi.Models;

namespace LogViewerApi.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<List<ProjectInfo>> ListProjectsAsync()
    {
        var projects = new List<ProjectInfo>();

        await foreach (var container in _blobServiceClient.GetBlobContainersAsync())
        {
            var lastModified = container.Properties.LastModified;
            projects.Add(new ProjectInfo(container.Name, lastModified));
        }

        return projects;
    }

    public async Task<RunListResponse?> ListRunsAsync(string projectId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);

        try
        {
            await containerClient.GetPropertiesAsync();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        var runGroups = new Dictionary<string, DateTimeOffset>();

        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            var firstSegment = blob.Name.Split('/')[0];
            var blobLastModified = blob.Properties.LastModified ?? DateTimeOffset.MinValue;

            if (runGroups.TryGetValue(firstSegment, out var existing))
            {
                if (blobLastModified > existing)
                {
                    runGroups[firstSegment] = blobLastModified;
                }
            }
            else
            {
                runGroups[firstSegment] = blobLastModified;
            }
        }

        var runs = runGroups
            .Select(g => new RunInfo(g.Key, g.Value))
            .ToList();

        return new RunListResponse(projectId, runs);
    }

    public async Task<bool> ProjectExistsAsync(string projectId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);

        try
        {
            await containerClient.GetPropertiesAsync();
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);

        var logs = new List<LogItemInfo>();
        var prompts = new List<LogItemInfo>();
        var artifacts = new List<LogItemInfo>();

        await foreach (var blob in containerClient.GetBlobsAsync(prefix: runId + "/"))
        {
            var relativeName = blob.Name[(runId.Length + 1)..];
            var size = blob.Properties.ContentLength ?? 0;
            var lastModified = blob.Properties.LastModified ?? DateTimeOffset.MinValue;

            if (relativeName.StartsWith("prompts/"))
            {
                var displayName = relativeName["prompts/".Length..];
                prompts.Add(new LogItemInfo(displayName, size, lastModified));
            }
            else if (relativeName.EndsWith(".log"))
            {
                logs.Add(new LogItemInfo(relativeName, size, lastModified));
            }
            else
            {
                artifacts.Add(new LogItemInfo(relativeName, size, lastModified));
            }
        }

        if (logs.Count == 0 && prompts.Count == 0 && artifacts.Count == 0)
        {
            return null;
        }

        return new LogListResponse(projectId, runId, logs, prompts, artifacts);
    }
}
