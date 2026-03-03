# User Journeys

## J-1: Smoke test — API surface and health
<!-- after: 1 -->
<!-- covers: health, openapi -->
<!-- tags: smoke -->
Hit GET /health → verify 200 with {"status":"ok"} → GET /openapi/v1.json → verify valid OpenAPI 3.0 document is returned with correct content type → GET /swagger → verify Swagger UI loads (200 or redirect to /swagger/index.html)

## J-2: Browse projects and discover runs
<!-- after: 2 -->
<!-- covers: projects.list, runs.list, error.handling -->
GET /projects → verify response contains projects array with id and last_modified fields → pick the first project → GET /projects/{projectId}/runs → verify runs array with id and last_modified → GET /projects/nonexistent-project/runs → verify 404 with {"error":"Project not found"}

## J-3: Explore run contents and log classification
<!-- after: 3 -->
<!-- covers: logs.list, logs.classify -->
GET /projects → pick a project → GET /projects/{projectId}/runs → pick a run → GET /projects/{projectId}/runs/{runId}/logs → verify response has three categories: logs (*.log files like orchestrator.log, builder-1.log), prompts (files from prompts/ sub-prefix), and artifacts (everything else: events.jsonl, run-metadata.json, *.done, validation-*.txt) → verify each item has name, size, and last_modified fields → GET /projects/{projectId}/runs/nonexistent-run/logs → verify 404 with {"error":"Run not found"}

## J-4: Read log content via JSON envelope
<!-- after: 4 -->
<!-- covers: projects.list, runs.list, logs.list, logs.content -->
GET /projects → pick project → GET runs → pick run → GET logs → pick an agent log (e.g. orchestrator.log) → GET /projects/{p}/runs/{r}/logs/orchestrator.log → verify JSON envelope with project_id, run_id, name, size, offset, last_modified, and content fields → verify offset equals size (full read) → verify content is non-empty string

## J-5: Follow a log with offset-based incremental reads
<!-- after: 4 -->
<!-- covers: logs.content, logs.content.offset -->
Navigate to a known log file → GET /projects/{p}/runs/{r}/logs/{file} → note the returned offset value → GET same URL with ?offset={offset} → verify content is empty string (no new data) and offset equals size → GET with ?offset=0 → verify full content is returned and offset equals size → GET with offset midway through file → verify partial content returned and offset equals size

## J-6: Raw content retrieval with range support
<!-- after: 4 -->
<!-- covers: logs.content.raw, logs.content.offset -->
Navigate to a log file → GET /projects/{p}/runs/{r}/logs/{file}?raw=true → verify response has appropriate Content-Type header and body is raw file content (not JSON) → GET same URL with ?raw=true&offset=100 → verify Content-Range header is present (e.g. bytes 100-N/total) and body contains only content from byte 100 onward

## J-7: Tail large spawn logs
<!-- after: 4 -->
<!-- covers: logs.tail, logs.list -->
Navigate to a run → GET logs → identify a large spawn log (e.g. builder-1-spawn.log) → GET /projects/{p}/runs/{r}/logs/builder-1-spawn.log/tail → verify default 100 lines returned with total_size, lines_returned, and content fields → GET same URL with ?lines=10 → verify lines_returned is 10 and content is shorter → verify content matches the end of the file

## J-8: Error handling consistency across all resource types
<!-- after: 4 -->
<!-- covers: error.handling -->
GET /projects/does-not-exist/runs → verify 404 {"error":"Project not found"} → GET /projects/{validProject}/runs/does-not-exist/logs → verify 404 {"error":"Run not found"} → GET /projects/{validProject}/runs/{validRun}/logs/no-such-file.log → verify 404 {"error":"Log not found"} → verify all error responses use consistent {"error":"<message>"} JSON envelope with no extra fields

## J-9: Access prompts and artifacts through log content endpoint
<!-- after: 4 -->
<!-- covers: logs.classify, logs.content -->
Navigate to a run → GET logs → note a prompt file name (e.g. prompts/planner-20260302-211620.txt) → GET /projects/{p}/runs/{r}/logs/prompts/planner-20260302-211620.txt → verify content retrieved successfully in JSON envelope → note an artifact file name (e.g. events.jsonl) → GET /projects/{p}/runs/{r}/logs/events.jsonl → verify artifact content retrieved → confirm both prompts and artifacts use the same logs/{fileName} endpoint path

## J-10: OpenAPI document completeness
<!-- after: 4 -->
<!-- covers: openapi, health, projects.list, runs.list, logs.list, logs.content, logs.tail -->
GET /openapi/v1.json → parse the OpenAPI document → verify all six endpoints are documented: /health, /projects, /projects/{projectId}/runs, /projects/{projectId}/runs/{runId}/logs, /projects/{projectId}/runs/{runId}/logs/{fileName}, /projects/{projectId}/runs/{runId}/logs/{fileName}/tail → verify query parameters raw, offset, and lines are described → verify response schemas include the expected fields (e.g. project_id, run_id, content, offset)

## J-11: Docker image build and Kubernetes readiness
<!-- after: 5 -->
<!-- covers: docker, kubernetes, health -->
Build Docker image using the multi-stage Dockerfile → run container with STORAGE_ACCOUNT_URL set → GET /health on port 8080 → verify {"status":"ok"} → inspect Kubernetes Deployment manifest: verify single replica, buildteam-sa service account, azure.workload.identity/use label, liveness/readiness probes on /health, STORAGE_ACCOUNT_URL env var → inspect Service manifest: verify port 80 targeting container port 8080

## J-12: Full project drill-down — end-to-end exploration
<!-- after: 5 -->
<!-- covers: projects.list, runs.list, logs.list, logs.classify, logs.content, logs.content.raw, logs.tail -->
GET /health → 200 → GET /projects → pick a project → GET /projects/{p}/runs → pick the latest run → GET /projects/{p}/runs/{r}/logs → verify logs, prompts, and artifacts arrays → pick an agent log → GET content (JSON envelope) → verify fields → GET same log with ?raw=true → verify raw body → GET same log /tail?lines=50 → verify 50 lines → pick a prompt file → GET content → verify prompt text → pick an artifact → GET content → verify artifact data → complete full top-to-bottom API exploration
