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
}
