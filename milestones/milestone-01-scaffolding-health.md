# Milestone: Scaffolding & health endpoint

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any value, e.g. `https://fake.blob.core.windows.net`), the following should work:
> - `GET /health` → 200 with body `{"status":"ok"}`
> - `GET /openapi/v1.json` → 200 with valid JSON containing `"openapi":"3` and paths including `/health`
> - `GET /swagger/index.html` → 200 with HTML content (Swagger UI)
> - `dotnet test` in the test project directory → all tests pass (at least one placeholder test)
> - The app should start without errors when `STORAGE_ACCOUNT_URL` is set

> **Reference files:** This is the first milestone — no prior feature files exist. The builder should follow SPEC.md for architecture and conventions.

## Tasks

- [ ] Create the solution file (`LogViewerApi.sln`) and the API project (`src/LogViewerApi/LogViewerApi.csproj`) targeting `net9.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- [ ] Add NuGet package references to `LogViewerApi.csproj`: `Azure.Identity`, `Azure.Storage.Blobs`, `Microsoft.AspNetCore.OpenApi`
- [ ] Create `src/LogViewerApi/Program.cs` with `WebApplication.CreateBuilder`, configure Kestrel to listen on `http://+:8080` by default, call `builder.Build()` and `app.Run()`
- [ ] Add `BlobServiceClient` singleton DI registration in `Program.cs` — read `STORAGE_ACCOUNT_URL` from environment, throw on missing value, construct `new BlobServiceClient(new Uri(url), new DefaultAzureCredential())`
- [ ] Add OpenAPI configuration in `Program.cs` — call `builder.Services.AddOpenApi()` and `app.MapOpenApi()` to serve the OpenAPI doc at `/openapi/v1.json`
- [ ] Add Swagger UI middleware in `Program.cs` — call `app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "Log Viewer API"); })` to serve Swagger UI at `/swagger`
- [ ] Add global exception handler in `Program.cs` — register `app.UseExceptionHandler` that catches all exceptions, checks for `RequestFailedException` (from `Azure.Storage.Blobs`), and returns `{"error":"Storage account unavailable: <detail>"}` for Azure errors or `{"error":"An unexpected error occurred"}` for others, with appropriate status codes (500)
- [ ] Create the directory structure: `src/LogViewerApi/Services/`, `src/LogViewerApi/Models/`, `src/LogViewerApi/Endpoints/`
- [ ] Create `src/LogViewerApi/Services/IBlobStorageService.cs` — empty interface `IBlobStorageService` in namespace `LogViewerApi.Services`
- [ ] Create `src/LogViewerApi/Services/BlobStorageService.cs` — class `BlobStorageService` implementing `IBlobStorageService`, accepting `BlobServiceClient` via constructor injection
- [ ] Register `IBlobStorageService` / `BlobStorageService` as scoped in DI in `Program.cs` — `builder.Services.AddScoped<IBlobStorageService, BlobStorageService>()`
- [ ] Create `src/LogViewerApi/Endpoints/HealthEndpoints.cs` — static class with `MapHealthEndpoints(this WebApplication app)` extension method that maps `GET /health` returning `Results.Ok(new { status = "ok" })`
- [ ] Wire up `app.MapHealthEndpoints()` call in `Program.cs` after middleware registration
- [ ] Create the xUnit test project (`tests/LogViewerApi.Tests/LogViewerApi.Tests.csproj`) targeting `net9.0` with package references to `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and a project reference to `src/LogViewerApi/LogViewerApi.csproj`
- [ ] Add the test project to the solution file (`LogViewerApi.sln`)
- [ ] Create `tests/LogViewerApi.Tests/HealthEndpointTests.cs` with one placeholder test method `HealthEndpoint_ReturnsOk` that asserts `true` (verifies the test framework runs)
