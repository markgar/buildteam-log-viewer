namespace LogViewerApi.Models;

public record BlobContentResult(string Content, long Size, long Offset, DateTimeOffset LastModified, string ContentType);
