# Log Viewer API

A read-only REST API that serves build-agent logs from Azure Blob Storage. Each blob container is a **project**, each timestamp-prefixed folder is a **run**, and the blobs inside are logs, prompts, and artifacts.

Runs on AKS with workload identity (`DefaultAzureCredential`) — no connection strings needed.

See [REQUIREMENTS.md](REQUIREMENTS.md) for full requirements and [SPEC.md](SPEC.md) for technical decisions.

## Build & Run

```bash
export STORAGE_ACCOUNT_URL=https://<account>.blob.core.windows.net
dotnet run --project src/LogViewerApi
```

Or with Docker:

```bash
docker build -t log-viewer-api .
docker run -e STORAGE_ACCOUNT_URL=https://<account>.blob.core.windows.net -p 8080:8080 log-viewer-api
```

## Develop

- .NET 9 SDK required
- Authenticate locally via `az login` (DefaultAzureCredential falls back to Azure CLI)
- Swagger UI available at `/swagger` when running
