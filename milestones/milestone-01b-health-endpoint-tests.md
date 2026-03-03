# Milestone: Health endpoint, service layer & tests

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any value, e.g. `https://fake.blob.core.windows.net`), the following should work:
> - `GET /health` → 200 with body `{"status":"ok"}`
> - `GET /openapi/v1.json` → paths include `/health`
> - `dotnet test` in the test project directory → all tests pass (at least one placeholder test)

> **Reference files:** Milestone 01a (project scaffolding & core configuration) must be completed first. The builder should follow SPEC.md for architecture and conventions.

## Tasks

- [x] Create `src/LogViewerApi/Services/IBlobStorageService.cs` — empty interface `IBlobStorageService` in namespace `LogViewerApi.Services`
- [x] Create `src/LogViewerApi/Services/BlobStorageService.cs` — class `BlobStorageService` implementing `IBlobStorageService`, accepting `BlobServiceClient` via constructor injection
- [ ] Register `IBlobStorageService` / `BlobStorageService` as scoped in DI in `Program.cs` — `builder.Services.AddScoped<IBlobStorageService, BlobStorageService>()`
- [ ] Create `src/LogViewerApi/Endpoints/HealthEndpoints.cs` — static class with `MapHealthEndpoints(this WebApplication app)` extension method that maps `GET /health` returning `Results.Ok(new { status = "ok" })`
- [ ] Wire up `app.MapHealthEndpoints()` call in `Program.cs` after middleware registration
- [ ] Create the xUnit test project (`tests/LogViewerApi.Tests/LogViewerApi.Tests.csproj`) targeting `net9.0` with package references to `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and a project reference to `src/LogViewerApi/LogViewerApi.csproj`
- [ ] Add the test project to the solution file (`LogViewerApi.sln`)
- [ ] Create `tests/LogViewerApi.Tests/HealthEndpointTests.cs` with one placeholder test method `HealthEndpoint_ReturnsOk` that asserts `true` (verifies the test framework runs)
