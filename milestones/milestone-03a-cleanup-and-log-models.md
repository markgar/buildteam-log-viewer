# Milestone: Cleanup and log response models

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - App builds and starts without errors (`dotnet build` succeeds, `GET /health` → 200)
> - `dotnet test` passes all existing tests
> - New model types `LogItemInfo` and `LogListResponse` exist in `src/LogViewerApi/Models/`

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Models/RunListResponse.cs` — existing response DTO record pattern
> - `tests/LogViewerApi.Tests/HealthEndpointIntegrationTests.cs` — integration test class
> - `tests/LogViewerApi.Tests/ErrorResponseSerializationTests.cs` — serialization test class
> - `tests/LogViewerApi.Tests/ServiceRegistrationTests.cs` — service registration test class

## Tasks

### Cleanup (open findings)

- [x] Add `using` keyword to `JsonDocument.Parse()` calls in test files — in `tests/LogViewerApi.Tests/HealthEndpointIntegrationTests.cs` change `var doc = JsonDocument.Parse(content);` to `using var doc = JsonDocument.Parse(content);`, and in `tests/LogViewerApi.Tests/ErrorResponseSerializationTests.cs` change `var doc = JsonDocument.Parse(json);` to `using var doc = JsonDocument.Parse(json);` (fixes #29)
- [x] Add `[Collection("EnvironmentTests")]` attribute to `HealthEndpointIntegrationTests` class in `tests/LogViewerApi.Tests/HealthEndpointIntegrationTests.cs` to prevent parallel execution with other tests that mutate `STORAGE_ACCOUNT_URL` (fixes #30)
- [x] Add `[Collection("EnvironmentTests")]` attribute to `ServiceRegistrationTests` class in `tests/LogViewerApi.Tests/ServiceRegistrationTests.cs`, and wrap each test method body in try/finally that saves `Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URL")` before and restores it in `finally` (fixes #31)

### Response DTOs

- [x] Create `Models/LogItemInfo.cs` — `public record LogItemInfo(string Name, long Size, DateTimeOffset LastModified)` in namespace `LogViewerApi.Models`
- [ ] Create `Models/LogListResponse.cs` — `public record LogListResponse(string ProjectId, string RunId, IReadOnlyList<LogItemInfo> Logs, IReadOnlyList<LogItemInfo> Prompts, IReadOnlyList<LogItemInfo> Artifacts)` in namespace `LogViewerApi.Models`
