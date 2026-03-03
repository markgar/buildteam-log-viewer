using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LogViewerApi.Models;

namespace LogViewerApi.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task CheckStorageHealthAsync(CancellationToken cancellationToken = default)
    {
        await _blobServiceClient.GetAccountInfoAsync(cancellationToken);
    }

    public async Task<List<ProjectInfo>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<ProjectInfo>();

        await foreach (var container in _blobServiceClient.GetBlobContainersAsync(cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lastModified = container.Properties.LastModified;
            projects.Add(new ProjectInfo(container.Name, lastModified));
        }

        return projects;
    }

    public async Task<List<RunInfo>?> ListRunsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);

        if (!await ContainerExistsAsync(containerClient, cancellationToken))
            return null;

        var runGroups = new Dictionary<string, DateTimeOffset>();

        await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        return runs;
    }

    public async Task<bool> ProjectExistsAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);
        return await ContainerExistsAsync(containerClient, cancellationToken);
    }

    private static async Task<bool> ContainerExistsAsync(BlobContainerClient containerClient, CancellationToken cancellationToken = default)
    {
        try
        {
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<BlobContentResult?> GetLogContentAsync(string projectId, string runId, string fileName, long offset, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);
        var blobPath = $"{runId}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        BlobProperties properties;
        try
        {
            properties = (await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        var blobSize = properties.ContentLength;
        var contentType = properties.ContentType ?? "application/octet-stream";
        var lastModified = properties.LastModified;

        if (offset >= blobSize)
        {
            return new BlobContentResult("", blobSize, blobSize, lastModified, contentType);
        }

        var downloadOptions = new BlobDownloadOptions
        {
            Range = new HttpRange(offset, blobSize - offset)
        };
        var download = await blobClient.DownloadStreamingAsync(downloadOptions, cancellationToken);
        using var reader = new StreamReader(download.Value.Content);
        var content = await reader.ReadToEndAsync(cancellationToken);

        return new BlobContentResult(content, blobSize, blobSize, lastModified, contentType);
    }

    public async Task<LogTailResponse?> GetLogTailAsync(string projectId, string runId, string fileName, int lines, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);
        var blobPath = $"{runId}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        BlobProperties properties;
        try
        {
            properties = (await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        var blobSize = properties.ContentLength;

        if (blobSize == 0)
        {
            return new LogTailResponse(projectId, runId, fileName, 0, 0, "");
        }

        var chunkSize = Math.Min(blobSize, 8192);
        string content;

        while (true)
        {
            var rangeStart = blobSize - chunkSize;
            var downloadOptions = new BlobDownloadOptions
            {
                Range = new HttpRange(rangeStart, chunkSize)
            };
            var download = await blobClient.DownloadStreamingAsync(downloadOptions, cancellationToken);
            using var reader = new StreamReader(download.Value.Content);
            content = await reader.ReadToEndAsync(cancellationToken);

            var newlineCount = content.Count(c => c == '\n');
            if (newlineCount >= lines + 1 || chunkSize >= blobSize)
            {
                break;
            }

            chunkSize = Math.Min(chunkSize * 2, blobSize);
        }

        var allLines = content.Split('\n');
        var tailLines = allLines.TakeLast(lines + 1).ToArray();

        // If the first element is empty (blob started with partial line from chunk boundary), skip it
        if (tailLines.Length > lines)
        {
            tailLines = tailLines.Skip(1).ToArray();
        }

        // Remove trailing empty line from final newline
        if (tailLines.Length > 0 && tailLines[^1] == "")
        {
            tailLines = tailLines[..^1];
        }

        var actualLines = tailLines.Length;
        var joinedContent = string.Join("\n", tailLines);

        return new LogTailResponse(projectId, runId, fileName, blobSize, actualLines, joinedContent);
    }

    public async Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(projectId);

        var logs = new List<LogItemInfo>();
        var prompts = new List<LogItemInfo>();
        var artifacts = new List<LogItemInfo>();

        try
        {
            await foreach (var blob in containerClient.GetBlobsAsync(prefix: runId + "/", cancellationToken: cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
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
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        if (logs.Count == 0 && prompts.Count == 0 && artifacts.Count == 0)
        {
            return null;
        }

        return new LogListResponse(projectId, runId, logs, prompts, artifacts);
    }
}
