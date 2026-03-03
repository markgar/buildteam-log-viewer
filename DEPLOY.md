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

- **`GET /health`** → 200 `{"status":"ok"}` when storage is reachable
- **`GET /health`** → 503 `{"error":"Storage account unreachable"}` when storage connectivity check fails
- Actively verifies blob storage by calling `GetAccountInfoAsync()`
- Mapped via `HealthEndpoints.MapHealthEndpoints()` extension method
- Listed in OpenAPI spec at `/openapi/v1.json`

## Verified Endpoints

- `GET /health` → 200 `{"status":"ok"}`
- `GET /openapi/v1.json` → 200 (valid OpenAPI 3.1.1 JSON document, content-type `application/json;charset=utf-8`, paths include `/health`, `/projects`, `/projects/{projectId}/runs`)
- `GET /swagger/index.html` → 200 (Swagger UI HTML page)
- `GET /projects` → 500 `{"error":"An unexpected error occurred"}` when storage unreachable (fake URL). **Bug:** Should return `{"error":"Storage account unavailable: ..."}` — see issue #35.
- `GET /projects/nonexistent-project-xyz/runs` → 500 when storage unreachable (expected behavior)
- `GET /projects/{projectId}/runs/{runId}/logs` → 500 when storage unreachable; 404 with `{"error":"Project not found"}` or `{"error":"Run not found"}` when storage reachable but resource missing
- `GET /projects/{projectId}/runs/{runId}/logs/{**fileName}` → 500 when storage unreachable; 404 with `{"error":"Project not found"}` or `{"error":"Log not found"}` when storage reachable but resource missing. Accepts `raw` and `offset` query params.
- `GET /projects/{projectId}/runs/{runId}/logs/{fileName}/tail` → 500 when storage unreachable; 404 with `{"error":"Project not found"}` or `{"error":"Log not found"}` when storage reachable but resource missing. Accepts `lines` query param.

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

## Log Response DTOs (milestone 03a)

New model records added for upcoming log list endpoint:
- `Models/LogItemInfo.cs` — `record LogItemInfo(string Name, long Size, DateTimeOffset LastModified)`
- `Models/LogListResponse.cs` — `record LogListResponse(string ProjectId, string RunId, IReadOnlyList<LogItemInfo> Logs, IReadOnlyList<LogItemInfo> Prompts, IReadOnlyList<LogItemInfo> Artifacts)`

## Test Cleanup (milestone 03a)

- Added `using` keyword to `JsonDocument.Parse()` calls in test files to prevent memory leaks
- Added `[Collection("EnvironmentTests")]` to `HealthEndpointIntegrationTests` and `ServiceRegistrationTests` to prevent parallel execution conflicts when mutating `STORAGE_ACCOUNT_URL`
- `ServiceRegistrationTests` now saves/restores `STORAGE_ACCOUNT_URL` in try/finally blocks

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

- **Unit tests:** `dotnet test LogViewerApi.sln` — runs 74 xUnit tests (health endpoint, OpenAPI, startup config, DI registration, error response serialization, response model serialization, project endpoint integration, run endpoint integration, run log endpoint cross-feature, log content endpoint, tail endpoint, exception handler integration, exception handler run logs integration). All pass as of milestone 05a.
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

## Log Endpoints (milestone 03b)

New endpoint added:
- `GET /projects/{projectId}/runs/{runId}/logs` — lists logs, prompts, and artifacts for a specific run. Returns `LogListResponse` (200), 404 with `{"error":"Project not found"}` or `{"error":"Run not found"}`, or 500 if storage unreachable.

Service layer additions:
- `ProjectExistsAsync(string projectId)` — checks if a container exists
- `ListRunLogsAsync(string projectId, string runId)` — lists blobs under a run prefix, classifies into logs/prompts/artifacts

Test stub fix:
- `StubBlobStorageService` must implement `ProjectExistsAsync` and `ListRunLogsAsync` — the milestone added these to `IBlobStorageService` but didn't update the stub, causing test compilation failure. Fixed by adding stub implementations.

Playwright test fix:
- The Swagger UI test for `/projects/{projectId}/runs` endpoint used `hasText` filter which matched both `/runs` and `/runs/{runId}/logs`. Fixed by using `data-path` attribute selector instead.

## Response DTOs (milestone 04a)

New model records added for upcoming log content and tail endpoints:
- `Models/LogContentResponse.cs` — `record LogContentResponse(string ProjectId, string RunId, string Name, long Size, long Offset, DateTimeOffset LastModified, string Content)` — JSON envelope for log file content retrieval
- `Models/LogTailResponse.cs` — `record LogTailResponse(string ProjectId, string RunId, string Name, long TotalSize, int LinesReturned, string Content)` — JSON envelope for tail endpoint
- `Models/BlobContentResult.cs` — `record BlobContentResult(string Content, long Size, long Offset, DateTimeOffset LastModified, string ContentType)` — internal service result carrying blob content and metadata

## Code Cleanup (milestone 04a)

- Extracted `ContainerExistsAsync(BlobContainerClient, CancellationToken)` private helper in `BlobStorageService` to deduplicate container-exists checks (fixes #47)
- Fixed env var save/restore in `ExceptionHandlerIntegrationTests` and `ProjectEndpointIntegrationTests` (fixes #46)
- Removed duplicate `validation-results.txt` entry from `.gitignore` (fixes #44)
- `StubBlobStorageService` now implements `ProjectExistsAsync` and `ListRunLogsAsync` with `LogsByProjectAndRun` dictionary (fixes #45)

## Fixed Bug — Health Endpoint (issue #54, fixed in milestone 04b)

The health endpoint previously caught only `RequestFailedException` but not `AuthenticationFailedException`. This caused 500 responses instead of the intended 503 when `DefaultAzureCredential` could not obtain a token. Fixed by adding a catch block for `AuthenticationFailedException` in `HealthEndpoints.cs`.

## Log Content & Tail Endpoints (milestone 04b)

New endpoints added:
- `GET /projects/{projectId}/runs/{runId}/logs/{**fileName}` — retrieves log file content. Accepts `raw` (bool, default false) and `offset` (long, default 0) query params. Returns `LogContentResponse` JSON envelope (200), 404 with `{"error":"Project not found"}` or `{"error":"Log not found"}`, or 500 if storage unreachable. When `raw=true`, returns raw text content with Content-Range header for offset reads.
- `GET /projects/{projectId}/runs/{runId}/logs/{fileName}/tail` — returns last N lines. Accepts `lines` (int, default 100) query param. Returns `LogTailResponse` (200), 404 with `{"error":"Project not found"}` or `{"error":"Log not found"}`, or 500 if storage unreachable.

Note: The tail endpoint uses `{fileName}` (not catch-all) so it matches simple filenames before the catch-all content route.

Service layer additions:
- `GetLogContentAsync(projectId, runId, fileName, offset)` — downloads blob content from offset using range reads
- `GetLogTailAsync(projectId, runId, fileName, lines)` — reads from end of blob, doubling chunk size until enough lines found

Test count: 74 xUnit tests (up from 51 in milestone 04a).

Playwright test fix:
- The Swagger UI test for `/projects/{projectId}/runs/{runId}/logs` endpoint used `hasText: '/logs'` which now matches 3 elements (logs list, content, tail). Fixed by using `data-path` attribute selector.

## Kubernetes Manifests (milestone 05a)

New files added:
- `k8s/deployment.yaml` — Kubernetes Deployment with `metadata.name: log-viewer-api`, workload identity label `azure.workload.identity/use: "true"`, `serviceAccountName: buildteam-sa`, container port 8080, `STORAGE_ACCOUNT_URL` env var (empty placeholder), liveness probe on `/health` (initialDelay 5s, period 10s), readiness probe on `/health` (initialDelay 3s, period 5s)
- `k8s/service.yaml` — Kubernetes ClusterIP Service exposing port 80 targeting container port 8080, selector `app: log-viewer-api`

### Known Bug — OpenAPI Response Schemas (issue #76)

The OpenAPI document at `/openapi/v1.json` documents all six endpoints and query parameters, but response schemas are empty (`"200": {"description": "OK"}`). The ASP.NET Minimal API OpenAPI generator does not emit response schemas unless `.Produces<T>()` is chained on endpoint definitions. Fields like `project_id`, `run_id`, `content`, `offset` are not described in the schema.

## Known Gotchas

- **Kestrel address conflict warning:** The app configures Kestrel to listen on port 8080 via `ConfigureKestrel` AND sets `ASPNETCORE_URLS`. This produces a warning: "Overriding address(es) 'http://+:8080'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead." It's harmless — the app still listens on 8080.
- **Playwright volume mounts:** Bind mounts don't work in this Docker-in-Docker environment. Bake e2e test files into a custom Docker image instead. Pin `@playwright/test` to `1.52.0` (exact) to match the Docker image `v1.52.0-noble`.
- **HEAD requests not supported on /openapi/v1.json:** `curl -I` returns 405 Method Not Allowed. Use `curl -sv` to inspect response headers from a GET request.
- **Duplicate endpoint/DI registrations (fixed in 01b):** The original code had `/health` mapped twice (in `HealthEndpoints.cs` and `Program.cs`) and `IBlobStorageService` registered twice (singleton and scoped). Both duplicates were removed — the endpoint lives in `HealthEndpoints.cs` and the service is registered as scoped.
- **Test project csproj must be in Dockerfile:** The solution file references `tests/LogViewerApi.Tests/LogViewerApi.Tests.csproj`. The Dockerfile must `COPY` this csproj before `dotnet restore` or the restore step fails.
