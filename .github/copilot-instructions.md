# Copilot Instructions

## About this codebase

This software is written with assistance from GitHub Copilot. The code is structured to be readable, modifiable, and extendable by Copilot (and other LLM-based agents). Every design decision should reinforce that.

### Guidelines for LLM-friendly code

- **Flat, explicit control flow.** Prefer straightforward if/else and early returns over deeply nested logic, complex inheritance hierarchies, or metaprogramming. Every function should be understandable from its source alone.
- **Small, single-purpose functions.** Keep functions short (ideally under ~40 lines). Each function does one thing with a clear name that describes it. This gives the LLM better context boundaries.
- **Descriptive naming over comments.** Variable and function names should make intent obvious. Use comments only when *why* isn't clear from the code — never to explain *what*.
- **Colocate related logic.** Keep constants, helpers, and the code that uses them close together (or in the same small file). Avoid scattering related pieces across many modules — LLMs work best when relevant context is nearby.
- **Consistent patterns.** When multiple functions do similar things, structure them identically. Consistent shape lets the LLM reliably extend the pattern.
- **No magic.** Avoid decorators that hide behavior, dynamic attribute access, implicit registration, or monkey-patching. Everything should be traceable by reading the code top-to-bottom.
- **Graceful error handling.** Wrap I/O and external calls in try/except (or the language's equivalent). Never let a transient failure crash the main workflow. Log the error and continue.
- **Minimal dependencies.** Only add a dependency when it provides substantial value. Fewer deps mean less surface area for the LLM to misunderstand.
- **One concept per file.** Each module owns a single concern. Don't mix unrelated responsibilities in the same file.
- **Design for testability.** Separate pure decision logic from I/O and subprocess calls so core functions can be tested without mocking. Pass dependencies as arguments rather than hard-coding them inside functions when practical. Keep side-effect-free helpers (parsing, validation, data transforms) in their own functions so they can be unit tested directly.

### Documentation maintenance

- When completing a task that changes the project structure, key files, architecture, or conventions, update `.github/copilot-instructions.md` to reflect the change.
- Keep the project-specific sections (Project structure, Key files, Architecture, Conventions) accurate and current.
- Never modify the coding guidelines or testing conventions sections above.
- This file is a **style guide**, not a spec. Describe file **roles** (e.g. 'server entry point'), not implementation details (e.g. 'uses List<T> with auto-incrementing IDs'). Conventions describe coding **patterns** (e.g. 'consistent JSON error envelope'), not implementation choices (e.g. 'store data in a static variable'). SPEC.md covers what to build — this file covers how to write code that fits the project.

## Project structure

Source code lives in `src/LogViewerApi/` — this is the primary directory to edit. It contains a single ASP.NET Core Minimal API project with subdirectories for `Endpoints/`, `Services/`, and `Models/`. Tests live in a separate xUnit project alongside the source. Kubernetes manifests and the Dockerfile live at the repository root or in a deployment directory.

## Key files

- `SPEC.md` — Technical specification defining the API surface, tech stack, and architecture
- `REQUIREMENTS.md` — Original project requirements and domain model
- `BACKLOG.md` — Ordered backlog of implementation milestones
- `README.md` — Project overview and getting-started guide
- `.gitignore` — Ignore rules for .NET build output, IDE files, and secrets

## Architecture

`Program.cs` is the single entry point — it registers DI services, configures middleware, and maps all endpoints. Endpoint classes are thin route definitions that accept an injected service interface, bind HTTP parameters, and return response DTOs; they contain no business logic. The service layer encapsulates all Azure Blob SDK interaction behind an interface, translating blob concepts (containers, prefixes, blobs) into domain concepts (projects, runs, logs). Models are plain C# record types used only for JSON serialization — they carry no behavior. Dependencies flow inward: Endpoints → Services → Models, with no reverse or circular references.

## Testing conventions

- **Use the project's test framework.** Plain functions with descriptive names.
- **Test the contract, not the implementation.** A test should describe expected behavior in terms a user would understand — not mirror the code's internal branching. If the test would break when you refactor internals without changing behavior, it's too tightly coupled.
- **Name tests as behavioral expectations.** `test_expired_token_triggers_refresh` not `test_check_token_returns_false`. The test name should read like a requirement.
- **Use realistic inputs.** Feed real-looking data, not minimal one-line synthetic strings. Edge cases should be things that could actually happen — corrupted inputs, empty files, missing fields.
- **Prefer regression tests.** When a bug is found, write the test that would have caught it before fixing it. This is the highest-value test you can write.
- **Don't test I/O wrappers.** Functions that just read a file and call a pure helper don't need their own tests — test the pure helper directly.
- **No mocking unless unavoidable.** Extract pure functions for testability so you don't need mocks. If you find yourself mocking, consider whether you should be testing a different function.

## Conventions

- **Consistent JSON error envelope.** All error responses use `{"error": "<message>"}` — same shape for 404s, 500s, and any future error codes.
- **Thin endpoints delegate to services.** Endpoint classes handle HTTP concerns only (parameter binding, status codes, content negotiation). All blob interaction and domain logic lives in the service layer behind an interface.
- **Response DTOs are records.** Use C# `record` types for all API response models — immutable, concise, and automatically serializable.
- **snake_case for JSON properties.** All JSON property names use snake_case (e.g. `last_modified`, `project_id`) to match the API contract, regardless of C# naming conventions.
- **Early-return on not-found.** Check for missing resources (container, prefix, blob) at the top of the endpoint handler and return 404 immediately rather than nesting the happy path.
- **Interface-based DI for services.** Register services by interface so endpoint handlers depend on abstractions. This keeps handlers testable and decoupled from the Azure SDK.
- **Environment variables for configuration.** No `appsettings.json` for secrets or deployment config — use environment variables exclusively and fail fast on startup if required values are missing.
- **Fluent API only for endpoint mapping.** Use Minimal API's fluent `MapGet` / `WithName` / `WithOpenApi` chain for route definitions — no controller classes or attribute routing.
