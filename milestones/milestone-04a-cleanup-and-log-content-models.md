# Milestone: Cleanup & log content models

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - App builds and starts without errors (`dotnet build` succeeds, `GET /health` → 200)
> - `dotnet test` passes all existing tests
> - `Models/LogContentResponse.cs`, `Models/LogTailResponse.cs`, and `Models/BlobContentResult.cs` exist and compile

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/IBlobStorageService.cs` — service interface with existing method signatures
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation showing blob enumeration and container-exists patterns
> - `src/LogViewerApi/Models/LogListResponse.cs` — response DTO record pattern
> - `tests/LogViewerApi.Tests/StubBlobStorageService.cs` — test stub implementing IBlobStorageService

## Tasks

### Cleanup — fix open findings

- [x] Fix StubBlobStorageService compilation: add `ProjectExistsAsync(string projectId)` returning `Task<bool>` (check if `RunsByProject.ContainsKey(projectId)`) and `ListRunLogsAsync(string projectId, string runId)` returning `Task<LogListResponse?>` (return null) to satisfy `IBlobStorageService` interface — add `Dictionary<string, LogListResponse> LogsByProjectAndRun` property for LogListResponse lookups (fixes #45)
- [ ] Remove duplicate `validation-results.txt` entry from `.gitignore` — the file currently has this line twice at the end (fixes #44)
- [ ] Extract private `ContainerExistsAsync(BlobContainerClient containerClient)` helper in `BlobStorageService` returning `Task<bool>` — try `containerClient.GetPropertiesAsync()` and return `true`, catch `RequestFailedException` when `Status == 404` and return `false`, then refactor `ProjectExistsAsync` and `ListRunsAsync` to call this helper instead of duplicating the try/catch pattern (fixes #47)
- [ ] Save/restore `STORAGE_ACCOUNT_URL` env var in `ExceptionHandlerIntegrationTests` and `ProjectEndpointIntegrationTests` — add `private readonly string? _savedStorageAccountUrl` field, save current value in constructor before overwriting, restore in `Dispose()` via `Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URL", _savedStorageAccountUrl)` to match the pattern already used in `CustomWebApplicationFactory` (fixes #46)

### Response DTOs

- [ ] Create `Models/LogContentResponse.cs` — `public record LogContentResponse(string ProjectId, string RunId, string Name, long Size, long Offset, DateTimeOffset LastModified, string Content)` matching JSON envelope fields: `project_id`, `run_id`, `name`, `size`, `offset`, `last_modified`, `content` (snake_case serialization handled by global JsonNamingPolicy)
- [ ] Create `Models/LogTailResponse.cs` — `public record LogTailResponse(string ProjectId, string RunId, string Name, long TotalSize, int LinesReturned, string Content)` matching JSON fields: `project_id`, `run_id`, `name`, `total_size`, `lines_returned`, `content`
- [ ] Create `Models/BlobContentResult.cs` — `public record BlobContentResult(string Content, long Size, long Offset, DateTimeOffset LastModified, string ContentType)` — internal service result used by GetLogContentAsync, carries ContentType from blob properties for raw mode response headers (not directly serialized as API response)
