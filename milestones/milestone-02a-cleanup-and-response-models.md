# Milestone: Cleanup & response models

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `GET /health` → 200 with `{"status":"ok"}`
> - `GET /openapi/v1.json` → 200, health endpoint is present
> - `GET /swagger/index.html` → 200 with HTML
> - Setting `PORT` to a non-integer or out-of-range value causes startup failure with a descriptive error message

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/IBlobStorageService.cs` — service interface (currently empty)
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation with injected `BlobServiceClient`
> - `src/LogViewerApi/Models/ErrorResponse.cs` — existing record DTO pattern
> - `src/LogViewerApi/Endpoints/HealthEndpoints.cs` — existing endpoint extension method pattern

## Tasks

### Cleanup (open findings)

- [x] Remove duplicate registrations from `Program.cs` — delete the inline `app.MapGet("/health", ...)` block (lines 70-72, already mapped by `app.MapHealthEndpoints()`) and delete the duplicate `builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>()` (line 28, keeping only the `AddScoped` registration on line 37)
- [x] Add `.WithOpenApi()` to the health endpoint fluent chain in `Endpoints/HealthEndpoints.cs` so the `MapGet` call ends with `.WithName("Health").WithOpenApi()` (fixes #14)
- [x] Validate PORT environment variable in `Program.cs` — when `PORT` is set but not a valid integer between 1 and 65535, throw `InvalidOperationException` with message `"PORT environment variable must be an integer between 1 and 65535, got: '{value}'"` instead of silently falling back to 8080 (fixes #16)
- [x] Nest `LogViewerApi` project under the `src` solution folder in `LogViewerApi.sln` — add `{9456D2AB-019D-449A-BA24-38F6E68EF9EB} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}` to the `NestedProjects` section (fixes #13)

### Response DTOs

- [x] Create `Models/ProjectInfo.cs` — `public record ProjectInfo(string Id, DateTimeOffset LastModified)` in namespace `LogViewerApi.Models`
- [ ] Create `Models/ProjectListResponse.cs` — `public record ProjectListResponse(IReadOnlyList<ProjectInfo> Projects)` in namespace `LogViewerApi.Models`
- [ ] Create `Models/RunInfo.cs` — `public record RunInfo(string Id, DateTimeOffset LastModified)` in namespace `LogViewerApi.Models`
- [ ] Create `Models/RunListResponse.cs` — `public record RunListResponse(string ProjectId, IReadOnlyList<RunInfo> Runs)` in namespace `LogViewerApi.Models`
