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
}
