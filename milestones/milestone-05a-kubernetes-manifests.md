# Milestone: Kubernetes manifests

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `k8s/deployment.yaml` exists and contains a Deployment with `metadata.name: log-viewer-api`, `spec.template.metadata.labels` including `azure.workload.identity/use: "true"`, `spec.template.spec.serviceAccountName: buildteam-sa`, container port 8080, `STORAGE_ACCOUNT_URL` env var, liveness and readiness probes on `/health`
> - `k8s/service.yaml` exists and contains a Service with port 80 targeting container port 8080
> - `Dockerfile` exists (already in place from prior milestones) and builds successfully
> - `GET /health` → 200 `{"status":"ok"}` (app starts and responds)
> - App builds and starts without errors

> **Reference files:**
> - `Dockerfile` — existing multi-stage Dockerfile (already complete, no changes needed)
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping

## Tasks

### Kubernetes manifests

- [x] Create `k8s/deployment.yaml` — Kubernetes Deployment manifest with `apiVersion: apps/v1`, `kind: Deployment`, `metadata.name: log-viewer-api`, `spec.replicas: 1`, `spec.selector.matchLabels: { app: log-viewer-api }`, `spec.template.metadata.labels: { app: log-viewer-api, azure.workload.identity/use: "true" }`, `spec.template.spec.serviceAccountName: buildteam-sa`, single container named `log-viewer-api` with `image: log-viewer-api:latest`, `containerPort: 8080`, env var `STORAGE_ACCOUNT_URL` with `value: ""` placeholder, `livenessProbe: { httpGet: { path: /health, port: 8080 }, initialDelaySeconds: 5, periodSeconds: 10 }`, `readinessProbe: { httpGet: { path: /health, port: 8080 }, initialDelaySeconds: 3, periodSeconds: 5 }`
- [x] Create `k8s/service.yaml` — Kubernetes Service manifest with `apiVersion: v1`, `kind: Service`, `metadata.name: log-viewer-api`, `spec.selector: { app: log-viewer-api }`, `spec.ports: [{ port: 80, targetPort: 8080, protocol: TCP }]`, `spec.type: ClusterIP`
