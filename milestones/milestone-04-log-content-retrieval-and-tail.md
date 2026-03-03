# Milestone: Log content retrieval & tail

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `GET /projects/nonexistent/runs/20260302-211501/logs/builder-1.log` → 404 with `{"error":"Project not found"}` or 500 if storage unreachable
> - `GET /projects/some-project/runs/20260302-211501/logs/nonexistent.log` → 404 with `{"error":"Log not found"}` or 500 if storage unreachable
> - `GET /projects/some-project/runs/20260302-211501/logs/nonexistent.log?raw=true` → 404 with `{"error":"Log not found"}` or 500
> - `GET /projects/some-project/runs/20260302-211501/logs/builder-1.log/tail` → 404 with `{"error":"Log not found"}` or 500
> - `GET /projects/some-project/runs/20260302-211501/logs/builder-1.log/tail?lines=50` → 404 or 500 (query param accepted without error)
> - `GET /openapi/v1.json` → 200, response contains paths for `/projects/{projectId}/runs/{runId}/logs/{fileName}` and tail endpoint
> - App builds and starts without errors (`dotnet build` succeeds, `GET /health` → 200)
> - `dotnet test` passes all existing tests

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/IBlobStorageService.cs` — service interface with existing method signatures
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation showing blob enumeration and container-exists patterns
> - `src/LogViewerApi/Models/LogListResponse.cs` — response DTO record pattern
> - `src/LogViewerApi/Endpoints/RunEndpoints.cs` — endpoint extension method pattern with early-return 404
> - `tests/LogViewerApi.Tests/StubBlobStorageService.cs` — test stub implementing IBlobStorageService

## Tasks

### Cleanup — fix open findings

- [ ] Fix StubBlobStorageService compilation: add `ProjectExistsAsync(string projectId)` returning `Task<bool>` (check if `RunsByProject.ContainsKey(projectId)`) and `ListRunLogsAsync(string projectId, string runId)` returning `Task<LogListResponse?>` (return null) to satisfy `IBlobStorageService` interface — add `Dictionary<string, LogListResponse> LogsByProjectAndRun` property for LogListResponse lookups (fixes #45)
- [ ] Remove duplicate `validation-results.txt` entry from `.gitignore` — the file currently has this line twice at the end (fixes #44)
- [ ] Extract private `ContainerExistsAsync(BlobContainerClient containerClient)` helper in `BlobStorageService` returning `Task<bool>` — try `containerClient.GetPropertiesAsync()` and return `true`, catch `RequestFailedException` when `Status == 404` and return `false`, then refactor `ProjectExistsAsync` and `ListRunsAsync` to call this helper instead of duplicating the try/catch pattern (fixes #47)
- [ ] Save/restore `STORAGE_ACCOUNT_URL` env var in `ExceptionHandlerIntegrationTests` and `ProjectEndpointIntegrationTests` — add `private readonly string? _savedStorageAccountUrl` field, save current value in constructor before overwriting, restore in `Dispose()` via `Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl)` to match the pattern already used in `CustomWebApplicationFactory` (fixes #46)

### Response DTOs

- [ ] Create `Models/LogContentResponse.cs` — `public record LogContentResponse(string ProjectId, string RunId, string Name, long Size, long Offset, DateTimeOffset LastModified, string Content)` matching JSON envelope fields: `project_id`, `run_id`, `name`, `size`, `offset`, `last_modified`, `content` (snake_case serialization handled by global JsonNamingPolicy)
- [ ] Create `Models/LogTailResponse.cs` — `public record LogTailResponse(string ProjectId, string RunId, string Name, long TotalSize, int LinesReturned, string Content)` matching JSON fields: `project_id`, `run_id`, `name`, `total_size`, `lines_returned`, `content`
- [ ] Create `Models/BlobContentResult.cs` — `public record BlobContentResult(string Content, long Size, long Offset, DateTimeOffset LastModified, string ContentType)` — internal service result used by GetLogContentAsync, carries ContentType from blob properties for raw mode response headers (not directly serialized as API response)

### Service layer

- [ ] Add `Task<BlobContentResult?> GetLogContentAsync(string projectId, string runId, string fileName, long offset)` to `IBlobStorageService` and implement in `BlobStorageService` — get container client, build blob path as `$"{runId}/{fileName}"`, get `BlobClient`, call `GetPropertiesAsync()` wrapped in try/catch for 404 (return null when blob not found), get `blobSize` from properties `ContentLength`, get `contentType` from properties `ContentType` (default `"application/octet-stream"`), get `lastModified` from properties `LastModified`, if offset >= blobSize return empty content result `new BlobContentResult("", blobSize, blobSize, lastModified, contentType)`, otherwise call `DownloadAsync(new BlobDownloadOptions { Range = new HttpRange(offset, blobSize - offset) })` to download from offset, read response stream to string using `StreamReader`, return `new BlobContentResult(content, blobSize, blobSize, lastModified, contentType)` where Offset = blobSize (points past last byte for follow pattern)
- [ ] Add `Task<LogTailResponse?> GetLogTailAsync(string projectId, string runId, string fileName, int lines)` to `IBlobStorageService` and implement in `BlobStorageService` — get container client, build blob path as `$"{runId}/{fileName}"`, get `BlobClient`, call `GetPropertiesAsync()` wrapped in try/catch for 404 (return null), get `blobSize` from properties `ContentLength`, read from end using range reads: start with `Math.Min(blobSize, 8192)` byte chunk from end via `DownloadAsync(new BlobDownloadOptions { Range = new HttpRange(blobSize - chunkSize, chunkSize) })`, convert to string, split on `'\n'`, if fewer than `lines + 1` newlines found and more bytes remain, double chunk size and re-read, repeat until enough lines or entire blob consumed, take last N lines, return `new LogTailResponse(projectId, runId, fileName, blobSize, actualLinesReturned, joinedContent)`

### Endpoints

- [ ] Create `Endpoints/LogEndpoints.cs` with static class `LogEndpoints` and `MapLogEndpoints(this WebApplication app)` extension method — add `GET /projects/{projectId}/runs/{runId}/logs/{**fileName}` route: accept `bool raw = false` and `long offset = 0` query params, inject `IBlobStorageService`, first call `ProjectExistsAsync(projectId)` and return `Results.NotFound(new ErrorResponse("Project not found"))` if false, then call `GetLogContentAsync(projectId, runId, fileName, offset)` and return `Results.NotFound(new ErrorResponse("Log not found"))` if null, when `raw == false` return `Results.Ok(new LogContentResponse(projectId, runId, fileName, result.Size, result.Offset, result.LastModified, result.Content))`, chain `.WithName("GetLogContent").WithOpenApi()`
- [ ] Add raw mode to the `GET /logs/{**fileName}` handler in `LogEndpoints` — when `raw == true`, return `Results.Text(result.Content, result.ContentType)`, if `offset > 0` set `Content-Range` response header to `$"bytes {offset}-{result.Size - 1}/{result.Size}"` before returning (use `context.Response.Headers["Content-Range"]` or return a custom `IResult`)
- [ ] Add `GET /projects/{projectId}/runs/{runId}/logs/{fileName}/tail` route to `LogEndpoints.MapLogEndpoints` — accept `int lines = 100` query param, inject `IBlobStorageService`, call `ProjectExistsAsync(projectId)` and return 404 `"Project not found"` if false, call `GetLogTailAsync(projectId, runId, fileName, lines)` and return `Results.NotFound(new ErrorResponse("Log not found"))` if null, otherwise return `Results.Ok(result)`, chain `.WithName("TailLog").WithOpenApi()` — note: this route uses `{fileName}` (not catch-all) so it matches simple filenames like `builder-1.log` before the catch-all content route

### Wiring

- [ ] Register `app.MapLogEndpoints()` in `Program.cs` after the existing `app.MapRunEndpoints()` call
- [ ] Add `GetLogContentAsync` and `GetLogTailAsync` stubs to `StubBlobStorageService` in test project — add `Dictionary<string, BlobContentResult> ContentByKey` and `Dictionary<string, LogTailResponse> TailByKey` properties, implement `GetLogContentAsync(projectId, runId, fileName, offset)` to look up `$"{projectId}/{runId}/{fileName}"` in ContentByKey (throw if ExceptionToThrow set, return null if not found), implement `GetLogTailAsync(projectId, runId, fileName, lines)` similarly using TailByKey
