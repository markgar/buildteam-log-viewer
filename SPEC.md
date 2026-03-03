# Technical Specification

## Summary

Log Viewer API is a read-only REST API that exposes build-agent logs stored in Azure Blob Storage. It maps blob containers to projects, timestamp-prefixed virtual folders to runs, and individual blobs to log files, prompts, and artifacts. The API is designed for internal use on AKS — no auth on the API itself, no caching, no write operations.

## Tech Stack

- **Language / Runtime:** C# / .NET 9
- **Framework:** ASP.NET Core Minimal API (top-level `Program.cs`, no controllers)
- **Azure Auth:** `Azure.Identity` — `DefaultAzureCredential` (workload identity on AKS, CLI locally)
- **Storage SDK:** `Azure.Storage.Blobs` — `BlobServiceClient` injected via DI
- **OpenAPI:** Built-in `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` / `MapOpenApi()`)
- **Container:** Multi-stage Dockerfile — `dotnet/sdk:9.0` build, `dotnet/aspnet:9.0` runtime

## Architecture

Single-project Minimal API. No separate class library — the codebase is small enough for one project.

```
src/LogViewerApi/
  Program.cs            # Host setup, DI, endpoint mapping
  Services/             # BlobStorageService — all Azure SDK calls
  Models/               # Response DTOs (records)
  Endpoints/            # Static classes grouping endpoint definitions
```

**Layers:**
1. **Endpoints** — route definitions, parameter binding, HTTP concerns only
2. **Services** — business logic and blob SDK interaction; injected via interface
3. **Models** — plain record types for JSON responses

**Dependency rule:** Endpoints depend on Services; Services depend on Models. No circular references.

## Cross-Cutting Concerns

**Azure authentication:** `BlobServiceClient` registered as singleton in DI, constructed with `DefaultAzureCredential` and the `STORAGE_ACCOUNT_URL` env var. All blob access flows through this single client.

**Error handling:** Global exception handler catches `RequestFailedException` (Azure SDK) and maps to consistent `{"error": "..."}` JSON. Domain-level 404s (project/run/log not found) are returned explicitly from endpoint handlers.

**Configuration:** Environment variables only — no `appsettings.json` for secrets. `STORAGE_ACCOUNT_URL` is required; startup fails fast if missing.

**Logging:** Default ASP.NET Core logging. No structured logging library needed at this stage.

## Acceptance Criteria

- Health endpoint returns 200 — usable as K8s probe
- Projects can be listed (each blob container appears as a project)
- Runs within a project can be listed (timestamp-prefixed folders discovered dynamically)
- Logs/prompts/artifacts within a run can be listed, classified by type
- Individual file content can be retrieved (JSON envelope or raw mode)
- Offset-based incremental reads work (follow pattern for live logs)
- Tail endpoint returns last N lines without downloading entire blob
- OpenAPI doc served at `/openapi/v1.json`; Swagger UI at `/swagger`
- Docker image builds and runs successfully
- Consistent error responses (404 / 500) with JSON envelope
