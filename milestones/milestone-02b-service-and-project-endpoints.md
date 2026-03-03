# Milestone: Service layer & project endpoints

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `GET /projects` → responds (200 with JSON `{"projects":[...]}` if storage is reachable, or 500 with `{"error":"Storage account unavailable: ..."}` if not)
> - `GET /projects/nonexistent-project-xyz/runs` → 404 with `{"error":"Project not found"}` if storage is reachable, or 500 if not
> - `GET /openapi/v1.json` → 200, response body contains paths `/projects` and `/projects/{projectId}/runs`

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/IBlobStorageService.cs` — service interface
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation with injected `BlobServiceClient`
> - `src/LogViewerApi/Models/` — response DTOs (ProjectInfo, ProjectListResponse, RunInfo, RunListResponse, ErrorResponse)
> - `src/LogViewerApi/Endpoints/HealthEndpoints.cs` — existing endpoint extension method pattern

## Tasks

### Service layer

- [x] Add `Task<List<ProjectInfo>> ListProjectsAsync()` to `IBlobStorageService` and implement in `BlobStorageService` — enumerate all containers via `_blobServiceClient.GetBlobContainersAsync()`, map each `BlobContainerItem` to `new ProjectInfo(item.Name, item.Properties.LastModified ?? DateTimeOffset.MinValue)`, return the list
- [x] Add `Task<RunListResponse?> ListRunsAsync(string projectId)` to `IBlobStorageService` and implement in `BlobStorageService` — get container client via `_blobServiceClient.GetBlobContainerClient(projectId)`, verify container exists by calling `containerClient.GetPropertiesAsync()` wrapped in try/catch for `RequestFailedException` with `Status == 404` (return `null` on not found), list all blobs via `containerClient.GetBlobsAsync()`, group by first path segment (split blob name on `"/"` and take first element), compute max `LastModified` per group, construct `RunInfo` for each group with the segment as `Id` and max date as `LastModified`, return `new RunListResponse(projectId, runs)`

### Endpoints

- [x] Create `Endpoints/ProjectEndpoints.cs` with static class and `MapProjectEndpoints(this WebApplication app)` extension method containing `GET /projects` — inject `IBlobStorageService`, call `ListProjectsAsync()`, return `Results.Ok(new ProjectListResponse(projects))`, chain `.WithName("ListProjects").WithOpenApi()`
- [x] Add `GET /projects/{projectId}/runs` route to `ProjectEndpoints.MapProjectEndpoints` — inject `IBlobStorageService`, call `ListRunsAsync(projectId)`, if result is `null` return `Results.NotFound(new ErrorResponse("Project not found"))`, otherwise return `Results.Ok(result)`, chain `.WithName("ListRuns").WithOpenApi()`
- [ ] Register project endpoints in `Program.cs` — add `app.MapProjectEndpoints()` call after `app.MapHealthEndpoints()`
