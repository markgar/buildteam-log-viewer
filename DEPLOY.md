# DEPLOY.md — Deployment Guide

## Dockerfile

- **Location:** `Dockerfile` (repo root)
- **Multi-stage build:** `mcr.microsoft.com/dotnet/sdk:10.0` for build, `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime
- **Note:** The project targets `net10.0` (not `net9.0` as SPEC.md states). Use .NET 10 SDK/runtime images.
- **Build steps:** Copies solution + all csproj files (both `src/LogViewerApi/` and `tests/LogViewerApi.Tests/`) first for layer caching, then `dotnet restore`, then copies all source and runs `dotnet publish`.
- **Important:** The solution file references both the main project and the test project. The Dockerfile must copy both `.csproj` files before `dotnet restore`, otherwise restore fails with "project file not found".

## Required Environment Variables

| Variable | Required | Value | Description |
|---|---|---|---|
| `STORAGE_ACCOUNT_URL` | Yes | e.g. `https://fake.blob.core.windows.net` | Storage account URL. App fails fast on startup if missing. |
| `ASPNETCORE_URLS` | No | `http://+:8080` | Already set in Dockerfile. Kestrel also configured in code to listen on 8080. |

## Port Mappings

- **Container port:** 8080
- **Host port (validation):** 7202
- Mapping: `7202:8080`

## Docker Compose

- **File:** `docker-compose.yml` (repo root)
- **Services:**
  - `app` — the API service (builds from Dockerfile, exposes 7202:8080)
  - `playwright` — test runner (profile `test`, uses `mcr.microsoft.com/playwright:v1.52.0-noble`)
- **Project name:** Set `COMPOSE_PROJECT_NAME=buildteam-log-viewer` before running compose commands
- **Note:** Volume bind mounts from the host do NOT work in this environment (Docker-in-Docker). Use `docker cp` or bake files into a custom image for Playwright tests.

## Startup Sequence

1. `docker compose build` — builds the app image
2. `docker compose up -d` — starts the app
3. App is healthy within 1-2 seconds
4. Verify with: `curl http://localhost:7202/health` (should return 200 `{"status":"ok"}`)

## Health Check

- **`GET /health`** → 200 `{"status":"ok"}` — fully implemented as of milestone 01b
- Mapped via `HealthEndpoints.MapHealthEndpoints()` extension method
- Listed in OpenAPI spec at `/openapi/v1.json`

## Verified Endpoints

- `GET /health` → 200 `{"status":"ok"}`
- `GET /openapi/v1.json` → 200 (valid OpenAPI 3.1.1 JSON document, content-type `application/json;charset=utf-8`, paths include `/health`, `/projects`, `/projects/{projectId}/runs`)
- `GET /swagger/index.html` → 200 (Swagger UI HTML page)
- `GET /projects` → 500 `{"error":"An unexpected error occurred"}` when storage unreachable (fake URL). **Bug:** Should return `{"error":"Storage account unavailable: ..."}` — see issue #35.
- `GET /projects/nonexistent-project-xyz/runs` → 500 when storage unreachable (expected behavior)

## PORT Validation

- Setting `PORT` to a non-integer (e.g. `notanumber`) causes startup failure with `InvalidOperationException: PORT environment variable must be an integer between 1 and 65535, got: 'notanumber'`
- Setting `PORT` to an out-of-range value (e.g. `99999`) causes the same failure
- When `PORT` is unset, app defaults to 8080

## Response DTOs (milestone 02a)

New model records added (no endpoints yet — these are data contracts for future milestones):
- `Models/ProjectInfo.cs` — `record ProjectInfo(string Id, DateTimeOffset LastModified)`
- `Models/ProjectListResponse.cs` — `record ProjectListResponse(IReadOnlyList<ProjectInfo> Projects)`
- `Models/RunInfo.cs` — `record RunInfo(string Id, DateTimeOffset LastModified)`
- `Models/RunListResponse.cs` — `record RunListResponse(string ProjectId, IReadOnlyList<RunInfo> Runs)`

## Project Endpoints (milestone 02b)

New endpoints added:
- `GET /projects` — lists all blob containers as projects. Returns `ProjectListResponse` (200) or 500 with error JSON if storage unreachable.
- `GET /projects/{projectId}/runs` — lists runs (first-segment blob groups) within a project. Returns `RunListResponse` (200), 404 with `{"error":"Project not found"}` if container doesn't exist, or 500 if storage unreachable.

Service layer:
- `IBlobStorageService` — interface with `ListProjectsAsync()` and `ListRunsAsync(string projectId)`
- `BlobStorageService` — implementation using `BlobServiceClient` injected via DI
- `ProjectEndpoints.cs` — maps `/projects` and `/projects/{projectId}/runs` routes

### Known Bug (issue #35)

The global exception handler in `Program.cs` only catches `Azure.RequestFailedException` to produce `"Storage account unavailable: ..."` error messages. When `DefaultAzureCredential` fails (e.g., no credentials in container), the exception is `Azure.Identity.CredentialUnavailableException` which falls through to the generic `"An unexpected error occurred"` handler. Fix: also catch `Azure.Identity.AuthenticationFailedException`.

## Running Tests

- **Unit tests:** `dotnet test LogViewerApi.sln` — runs 14 xUnit tests (health endpoint, OpenAPI, startup config, DI registration, error response serialization)
- **Playwright e2e:** Build a custom image with e2e files baked in, then run on the compose network:
  ```bash
  docker build -t pw-tests -f /tmp/Dockerfile.pw .
  docker run --rm --network buildteam-log-viewer_default -e BASE_URL=http://buildteam-log-viewer-app-1:8080 pw-tests npx playwright test --reporter=list
  ```
  The Dockerfile.pw is:
  ```dockerfile
  FROM mcr.microsoft.com/playwright:v1.52.0-noble
  WORKDIR /app/e2e
  COPY e2e/package.json ./
  RUN npm install
  COPY e2e/ ./
  ```

## Known Gotchas

- **Kestrel address conflict warning:** The app configures Kestrel to listen on port 8080 via `ConfigureKestrel` AND sets `ASPNETCORE_URLS`. This produces a warning: "Overriding address(es) 'http://+:8080'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead." It's harmless — the app still listens on 8080.
- **Playwright volume mounts:** Bind mounts don't work in this Docker-in-Docker environment. Bake e2e test files into a custom Docker image instead. Pin `@playwright/test` to `1.52.0` (exact) to match the Docker image `v1.52.0-noble`.
- **HEAD requests not supported on /openapi/v1.json:** `curl -I` returns 405 Method Not Allowed. Use `curl -sv` to inspect response headers from a GET request.
- **Duplicate endpoint/DI registrations (fixed in 01b):** The original code had `/health` mapped twice (in `HealthEndpoints.cs` and `Program.cs`) and `IBlobStorageService` registered twice (singleton and scoped). Both duplicates were removed — the endpoint lives in `HealthEndpoints.cs` and the service is registered as scoped.
- **Test project csproj must be in Dockerfile:** The solution file references `tests/LogViewerApi.Tests/LogViewerApi.Tests.csproj`. The Dockerfile must `COPY` this csproj before `dotnet restore` or the restore step fails.
