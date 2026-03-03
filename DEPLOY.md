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
- `GET /openapi/v1.json` → 200 (valid OpenAPI 3.1.1 JSON document, paths include `/health`)
- `GET /swagger/index.html` → 200 (Swagger UI HTML page)

## Running Tests

- **Unit tests:** `dotnet test LogViewerApi.sln` — runs 7 xUnit tests (health endpoint, OpenAPI, startup config)
- **Playwright e2e:** Build a custom image with e2e files baked in, then run on the compose network:
  ```bash
  docker build -t pw-tests -f /tmp/Dockerfile.pw .
  docker run --rm --network buildteam-log-viewer_default -e BASE_URL=http://buildteam-log-viewer-app-1:8080 pw-tests npx playwright test --reporter=list
  ```

## Known Gotchas

- **Kestrel address conflict warning:** The app configures Kestrel to listen on port 8080 via `ConfigureKestrel` AND sets `ASPNETCORE_URLS`. This produces a warning: "Overriding address(es) 'http://+:8080'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead." It's harmless — the app still listens on 8080.
- **Playwright volume mounts:** Bind mounts don't work in this Docker-in-Docker environment. Bake e2e test files into a custom Docker image instead. Pin `@playwright/test` to `1.52.0` (exact) to match the Docker image `v1.52.0-noble`.
- **Duplicate endpoint/DI registrations (fixed in 01b):** The original code had `/health` mapped twice (in `HealthEndpoints.cs` and `Program.cs`) and `IBlobStorageService` registered twice (singleton and scoped). Both duplicates were removed — the endpoint lives in `HealthEndpoints.cs` and the service is registered as scoped.
- **Test project csproj must be in Dockerfile:** The solution file references `tests/LogViewerApi.Tests/LogViewerApi.Tests.csproj`. The Dockerfile must `COPY` this csproj before `dotnet restore` or the restore step fails.
