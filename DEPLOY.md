# DEPLOY.md — Deployment Guide

## Dockerfile

- **Location:** `Dockerfile` (repo root)
- **Multi-stage build:** `mcr.microsoft.com/dotnet/sdk:10.0` for build, `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime
- **Note:** The project targets `net10.0` (not `net9.0` as SPEC.md states). Use .NET 10 SDK/runtime images.
- **Build steps:** Copies solution + csproj first for layer caching, then `dotnet restore`, then copies all source and runs `dotnet publish`.

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
- **Note:** Volume bind mounts from the host do NOT work in this environment (Docker-in-Docker). Use `docker cp` to copy files into containers instead.

## Startup Sequence

1. `docker compose build` — builds the app image
2. `docker compose up -d` — starts the app
3. App is healthy within 1-2 seconds
4. Verify with: `curl http://localhost:7202/openapi/v1.json` (should return 200)

## Health Check

- **No /health endpoint yet** (filed as issue #10). Use `/openapi/v1.json` as a health check proxy for now.
- Expected future endpoint: `GET /health` → 200 `{"status":"ok"}`

## Verified Endpoints

- `GET /openapi/v1.json` → 200 (valid OpenAPI 3.1.1 JSON document)
- `GET /swagger/index.html` → 200 (Swagger UI HTML page)

## Known Gotchas

- **Kestrel address conflict warning:** The app configures Kestrel to listen on port 8080 via `ConfigureKestrel` AND sets `ASPNETCORE_URLS`. This produces a warning: "Overriding address(es) 'http://+:8080'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead." It's harmless — the app still listens on 8080.
- **Playwright volume mounts:** Bind mounts don't work in this Docker-in-Docker environment. Use `docker cp` to copy test files into the playwright container. Pin `@playwright/test` to `1.52.0` (exact) to match the Docker image `v1.52.0-noble`.
- **Missing requirements from backlog story 1:** Health endpoint, IBlobStorageService interface/class, and xUnit test project are not yet implemented (issues #10, #11, #12).
