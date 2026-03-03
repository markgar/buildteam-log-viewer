# Milestone: Project scaffolding & core configuration

> **Validates:** After building and starting the app (with `STORAGE_ACCOUNT_URL` set to any value, e.g. `https://fake.blob.core.windows.net`), the following should work:
> - `GET /openapi/v1.json` → 200 with valid JSON containing `"openapi":"3`
> - `GET /swagger/index.html` → 200 with HTML content (Swagger UI)
> - The app should start without errors when `STORAGE_ACCOUNT_URL` is set

> **Reference files:** This is the first milestone — no prior feature files exist. The builder should follow SPEC.md for architecture and conventions.

## Tasks

- [x] Create the solution file (`LogViewerApi.sln`) and the API project (`src/LogViewerApi/LogViewerApi.csproj`) targeting `net10.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- [x] Add NuGet package references to `LogViewerApi.csproj`: `Azure.Identity`, `Azure.Storage.Blobs`, `Microsoft.AspNetCore.OpenApi`
- [x] Create `src/LogViewerApi/Program.cs` with `WebApplication.CreateBuilder`, configure Kestrel to listen on `http://+:8080` by default, call `builder.Build()` and `app.Run()`
- [x] Add `BlobServiceClient` singleton DI registration in `Program.cs` — read `STORAGE_ACCOUNT_URL` from environment, throw on missing value, construct `new BlobServiceClient(new Uri(url), new DefaultAzureCredential())`
- [x] Add OpenAPI configuration in `Program.cs` — call `builder.Services.AddOpenApi()` and `app.MapOpenApi()` to serve the OpenAPI doc at `/openapi/v1.json`
- [x] Add Swagger UI middleware in `Program.cs` — call `app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "Log Viewer API"); })` to serve Swagger UI at `/swagger`
- [x] Add global exception handler in `Program.cs` — register `app.UseExceptionHandler` that catches all exceptions, checks for `RequestFailedException` (from `Azure.Storage.Blobs`), and returns `{"error":"Storage account unavailable: <detail>"}` for Azure errors or `{"error":"An unexpected error occurred"}` for others, with appropriate status codes (500)
- [x] Create the directory structure: `src/LogViewerApi/Services/`, `src/LogViewerApi/Models/`, `src/LogViewerApi/Endpoints/`
