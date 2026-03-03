# Milestone: List run logs endpoint

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `GET /projects/nonexistent-project/runs/20260302-211501/logs` → 404 with `{"error":"Project not found"}` if storage is reachable, or 500 if not
> - `GET /projects/some-project/runs/nonexistent-run/logs` → 404 with `{"error":"Run not found"}` if storage is reachable, or 500 if not
> - `GET /openapi/v1.json` → 200, response body contains path `/projects/{projectId}/runs/{runId}/logs`
> - App builds and starts without errors (`dotnet build` succeeds, `GET /health` → 200)
> - `dotnet test` passes all existing tests

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/IBlobStorageService.cs` — service interface with existing method signatures
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation showing container-exists check and blob enumeration pattern
> - `src/LogViewerApi/Models/LogItemInfo.cs` — log item response DTO (created in milestone 03a)
> - `src/LogViewerApi/Models/LogListResponse.cs` — log list response DTO (created in milestone 03a)
> - `src/LogViewerApi/Endpoints/ProjectEndpoints.cs` — existing endpoint extension method pattern with early-return 404

## Tasks

### Service layer

- [x] Add `Task<bool> ProjectExistsAsync(string projectId)` to `IBlobStorageService` interface and implement in `BlobStorageService` — get container client via `_blobServiceClient.GetBlobContainerClient(projectId)`, call `containerClient.GetPropertiesAsync()` inside try/catch, return `true` on success, catch `RequestFailedException` with `Status == 404` and return `false` (same pattern as the container check in `ListRunsAsync`)
- [x] Add `Task<LogListResponse?> ListRunLogsAsync(string projectId, string runId)` to `IBlobStorageService` interface and implement in `BlobStorageService` — get container client, list blobs with prefix `runId + "/"` via `containerClient.GetBlobsAsync(prefix: runId + "/")`, collect into three lists (logs, prompts, artifacts), for each blob strip the `"{runId}/"` prefix from `blob.Name` to get the relative name, then classify: if relative name starts with `"prompts/"` → prompts list (use `relativeName["prompts/".Length..]` as display name), else if relative name ends with `".log"` → logs list (use relative name as-is), else → artifacts list (use relative name as-is), build `new LogItemInfo(displayName, blob.Properties.ContentLength ?? 0, blob.Properties.LastModified ?? DateTimeOffset.MinValue)` for each, if all three lists are empty return `null` (run not found), otherwise return `new LogListResponse(projectId, runId, logs, prompts, artifacts)`

### Endpoint

- [x] Create `Endpoints/RunEndpoints.cs` with static class and `MapRunEndpoints(this WebApplication app)` extension method containing `GET /projects/{projectId}/runs/{runId}/logs` — inject `IBlobStorageService`, first call `ProjectExistsAsync(projectId)` and if `false` return `Results.NotFound(new ErrorResponse("Project not found"))`, then call `ListRunLogsAsync(projectId, runId)` and if result is `null` return `Results.NotFound(new ErrorResponse("Run not found"))`, otherwise return `Results.Ok(result)`, chain `.WithName("ListRunLogs").WithOpenApi()`
- [ ] Register run endpoints in `Program.cs` — add `app.MapRunEndpoints()` call after `app.MapProjectEndpoints()`
