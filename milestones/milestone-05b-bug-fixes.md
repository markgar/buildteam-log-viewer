# Milestone: Bug fixes (open findings)

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any valid URL, e.g. `https://fake.blob.core.windows.net`):
> - `dotnet test LogViewerApi.sln` → all tests pass
> - `GET /projects/test/runs/run1/logs/file.log?offset=-1` → should not produce a 500 with misleading "Storage account unavailable" (negative offset validation)
> - `GET /projects/test/runs/run1/logs/file.log/tail?lines=0` or `lines=-5` → should be clamped to a valid value, not produce errors or empty results
> - App builds and starts without errors

> **Reference files:**
> - `src/LogViewerApi/Program.cs` — entry point, DI registration, endpoint mapping
> - `src/LogViewerApi/Services/BlobStorageService.cs` — service implementation with methods to fix (GetLogContentAsync, GetLogTailAsync)
> - `src/LogViewerApi/Endpoints/LogEndpoints.cs` — endpoint handlers with Content-Range and lines validation to fix
> - `tests/LogViewerApi.Tests/StubBlobStorageService.cs` — test stub to update for offset tracking

## Tasks

### Bug fixes

- [x] Validate negative offset in `GetLogContentAsync` (fixes #60) — in `src/LogViewerApi/Services/BlobStorageService.cs`, add an early guard at the top of `GetLogContentAsync`: `if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");`
- [x] Handle TOCTOU race in `GetLogContentAsync` download (fixes #61) — in `src/LogViewerApi/Services/BlobStorageService.cs`, wrap the `DownloadStreamingAsync` call (line ~119) in a `try/catch (RequestFailedException ex) when (ex.Status == 404)` that returns `null`, so a blob deleted between `GetPropertiesAsync` and `DownloadStreamingAsync` returns 404 instead of 500
- [x] Return 400 for negative offset in log content endpoint (completes #60) — in `src/LogViewerApi/Endpoints/LogEndpoints.cs`, in the `GET /projects/{projectId}/runs/{runId}/logs/{**fileName}` handler, catch `ArgumentOutOfRangeException` from the service call and return `Results.BadRequest(new ErrorResponse("Offset must be non-negative"))`, or add a guard before calling the service: `if (byteOffset < 0) return Results.BadRequest(new ErrorResponse("Offset must be non-negative"));`
- [x] Fix invalid Content-Range header when offset >= blob size (fixes #64) — in `src/LogViewerApi/Endpoints/LogEndpoints.cs`, change the Content-Range condition from `if (byteOffset > 0)` to `if (byteOffset > 0 && byteOffset < result.Size)` so the header is only set when there is actual ranged content (avoids invalid `bytes 500-499/500` headers per RFC 7233)
- [x] Fix GetLogTailAsync off-by-one for newline-terminated content (fixes #65) — in `src/LogViewerApi/Services/BlobStorageService.cs`, in `GetLogTailAsync`, move the trailing empty line removal (`if (tailLines[^1] == "") tailLines = tailLines[..^1]`) to happen on `allLines` BEFORE the `TakeLast(lines + 1)` call, so the trailing empty element from `Split('\n')` on newline-terminated content doesn't consume one of the requested line slots
- [x] Fix misleading comment in GetLogTailAsync (fixes #63) — in `src/LogViewerApi/Services/BlobStorageService.cs`, replace the comment `// If the first element is empty (blob started with partial line from chunk boundary), skip it` with `// Drop the first line if we have more than requested — it may be a partial line from the chunk boundary`
- [x] Add input validation for lines query parameter in tail endpoint (fixes #66) — in `src/LogViewerApi/Endpoints/LogEndpoints.cs`, change `var lineCount = lines ?? 100;` to `var lineCount = Math.Clamp(lines ?? 100, 1, 10000);` to prevent integer overflow from `lines + 1` in the service, reject zero/negative values, and cap excessive values that would force downloading entire blobs into memory
- [x] Update StubBlobStorageService to track requested offset (fixes #62) — in `tests/LogViewerApi.Tests/StubBlobStorageService.cs`, add `public long? LastRequestedOffset { get; private set; }` property, set `LastRequestedOffset = offset;` at the top of `GetLogContentAsync` before the key lookup, enabling tests to assert that the endpoint correctly passes the offset query parameter through to the service
