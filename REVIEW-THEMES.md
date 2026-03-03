# Review Themes

Last updated: Health endpoint, service layer & tests

1. **Pin dependency versions** — Never use floating/wildcard NuGet versions (`1.13.*`). Pin exact patch versions for reproducible builds. (#5)
2. **Validate configuration inputs at startup** — When reading environment variables, validate format (e.g., URI well-formedness) in addition to presence, and include the variable name in error messages. (#4)
3. **Use tooling for generated files** — Don't hand-craft files with strict format requirements (`.sln`, `.csproj`). Use `dotnet new sln` / `dotnet sln add` to avoid format issues like leading blank lines or placeholder GUIDs. (#1, #2)
4. **Keep docs in sync with code** — When deviating from spec (e.g., changing target framework from net9.0 to net10.0), update all documentation in the same commit: README, BACKLOG, milestone files, and SPEC. (#6, #20)
5. **Configure serialization conventions in scaffolding** — Establish global JSON serializer settings (naming policy, enum handling) during project setup, not later. Missing config causes silent contract violations that are hard to catch. (#7)
6. **Prefer overridable defaults over hard-coded values** — Use `UseUrls()` or environment-driven configuration instead of `ConfigureKestrel` with `ListenAnyIP`, so ports and bindings can be overridden at deployment time without code changes. (#3)
7. **Register each service and endpoint exactly once** — Duplicate DI registrations (especially with conflicting lifetimes) and duplicate route mappings cause silent overrides or runtime exceptions. When wiring up a new component, search for existing registrations before adding a new one. (#17, #18)
8. **Update Dockerfile when solution structure changes** — Adding projects to the solution requires updating Dockerfile COPY commands (or switching to project-specific restore) so the Docker build does not break on `dotnet restore`. (#19)
9. **Isolate tests from shared process state** — Tests that mutate process-wide state (environment variables, static fields) must use `[Collection]` to run sequentially or save/restore in try/finally to prevent cross-class race conditions in parallel xUnit execution. (#21)
