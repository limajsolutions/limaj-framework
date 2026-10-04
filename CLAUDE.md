# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

Limaj Framework is a reusable base for .NET 10 / Azure Functions SaaS backends. It contains two distinct, **independent** parts:

1. `packages/Limaj.Framework.*` — generic, domain-agnostic building blocks (the actual framework code).
2. `template-backend/` — a scaffold meant to be copied into new product repositories. It is **not** in the solution and most of its `src/`/`test/` directories are empty (a `.gitkeep` only, no `.csproj` files yet).

The framework packages are the only buildable code in the repo today.

## Common commands

Build the framework solution (only the five packages and their test projects are included):

```bash
dotnet build Limaj.Framework.sln
```

`Limaj.Framework.sln` has test projects under `packages/` (one per package, plus `Limaj.Framework.Architecture.Tests` for the dependency-direction check) — run them with `dotnet test Limaj.Framework.sln`. The test folders under `template-backend/test/` are still empty (`.gitkeep` only, no `.csproj` files yet) — do not invent a `dotnet test` workflow for `template-backend/` until real test projects exist there.

The template-backend's `.github/workflows/*.yml` are **placeholders** (`echo "TODO: ..."`). Don't treat them as a working CI mechanism.

## Architecture: package layering

Dependency direction is enforced and must not be broken:

| Package | Allowed dependencies |
|---|---|
| [Limaj.Framework.Core](packages/Limaj.Framework.Core/) | none (the result/error contract) |
| [Limaj.Framework.Abstractions](packages/Limaj.Framework.Abstractions/) | none (pure contracts/types) |
| [Limaj.Framework.Application](packages/Limaj.Framework.Application/) | `Abstractions` + `Core` only |
| [Limaj.Framework.Persistence.EFCore](packages/Limaj.Framework.Persistence.EFCore/) | `Abstractions` + `Core` + EF Core |
| [Limaj.Framework.Web](packages/Limaj.Framework.Web/) | `Abstractions` + `Core` + `Microsoft.AspNetCore.App` |

`Core` and `Abstractions` are siblings: neither references the other, and any package may reference `Core`. The actual references today are Application → `Abstractions` + `Core`, Persistence.EFCore → `Abstractions`, Web → `Core` only; [PackageDependencyDirectionTests](packages/Limaj.Framework.Architecture.Tests/PackageDependencyDirectionTests.cs) enforces the table and that Application references exactly `Abstractions` + `Core`.

**`Limaj.Framework.Core` charter:** dependency-free result/error contract only (`Result`, `Result<T>`, `Error`, `ErrorType`, the 3 framework exceptions and `IExceptionToErrorMapper`); any other type needs a DA.

`Application` must never reference concrete persistence, host identity, or infrastructure. If a concrete implementation is needed, define a contract in `Abstractions` and put the adapter in the appropriate infra-side package.

## Core patterns to reuse, not reinvent

- **Result / Error flow** — [Result.cs](packages/Limaj.Framework.Core/src/Result.cs) and [Error.cs](packages/Limaj.Framework.Core/src/Error.cs) in `Limaj.Framework.Core`. Application code returns `Result`/`Result<T>`; HTTP layer maps via [ResultExtensions.ToHttpResult](packages/Limaj.Framework.Web/src/Http/ResultExtensions.cs) which translates `ErrorType` → HTTP status (Validation→400, NotFound→404, Conflict→409, Forbidden→403, Unauthorized→401, TooManyRequests→429, Unexpected→500).
- **Exception bridge** — Domain code throws `DomainValidationException` / `NotFoundException` / `ConflictException` from [Errors/](packages/Limaj.Framework.Core/src/Errors/) (`Limaj.Framework.Core.Errors`); [RequestRunner.RunAsync](packages/Limaj.Framework.Web/src/Http/RequestRunner.cs) + [ExceptionExtensions](packages/Limaj.Framework.Web/src/Http/ExceptionExtensions.cs) wrap handlers and convert these to structured HTTP responses. New endpoints should be wrapped in `RequestRunner.RunAsync` rather than hand-written try/catch.
- **Entities & soft delete** — All persisted entities inherit [BaseEntity](packages/Limaj.Framework.Abstractions/src/Domain/BaseEntity.cs) (`Id`, `IsActive`, `CreatedAt`, `UpdatedAt`). [BaseRepository](packages/Limaj.Framework.Persistence.EFCore/src/Repositories/Base/BaseRepository.cs) implements soft delete via `IsActive` and offers `includeInactive` opt-ins on read methods; `HardDeleteByIdAsync` is the escape hatch. EF configurations should derive from [BaseEntityConfiguration](packages/Limaj.Framework.Persistence.EFCore/src/Persistence/Configurations/BaseEntityConfiguration.cs).
- **Unit of work** — [UnitOfWork&lt;TDbContext&gt;](packages/Limaj.Framework.Persistence.EFCore/src/UnitOfWork.cs) wraps `DbContext.Database.BeginTransactionAsync` with commit/rollback. Use it for multi-step writes; `SaveAsync` on the repository is fine for single-aggregate writes.
- **Identity** — Application code should depend on [IUserIdentityGateway](packages/Limaj.Framework.Abstractions/src/Identity/IUserIdentityGateway.cs), never on `HttpContext` or host-specific identity types.

## Guardrails (from README)

- No product/domain code, seed data, or business rules belong in `packages/Limaj.Framework.*`.
- If a concrete dependency is needed, add a contract to `Abstractions` and the adapter to a separate package — do not couple `Application` to infrastructure.
- The template scaffolding (`template-backend/`) is meant to be **copied** into a product repo and renamed (`Template` → product name in folders, `.csproj`, `.sln`, namespaces). Don't add product-specific examples to the template itself.

## AI tooling

Claude Code commands, agents, and skills no longer live in this repository — they live in https://github.com/thalleslima8/my-skills, distributed as a plugin marketplace. The starting point for a new project (a generic `.claude/settings.json` + a `CLAUDE.md` template) comes from https://github.com/thalleslima8/ai-starter-kit. This repository contributes only code (`packages/`) and the scaffold (`template-backend/`).

## Task management: epics in `docs/`

Products built from this template don't track work in an external board — `docs/epics/{backlog,in-progress,done}/` (versioned alongside the code) is the task-management system, formalized and driven by the commands of the `workflow` plugin (thalleslima8/my-skills), when installed in the project. The framework repo ships the empty folder scaffold plus a starter `docs/README.md` at `template-backend/docs/` (copied as-is into new products); the convention itself — folder lifecycle, `- [ ]`/`- [x]`, `DA-###` decisions, `Última revisão` — is documented in [docs/template-usage.md](docs/template-usage.md). This repo's own `docs/` only holds that template-usage guidance — there are no product epics here since the framework itself has no product code.

## Dev Container (template-backend)

`template-backend/.devcontainer/` is a **parametrizable** isolated dev environment (the Docker socket is NOT mounted, so the Claude allowlist can be broad safely — it lives only in `claude-settings.json` inside the container, never merged into the host's versioned `.claude/settings.json`). It is composed from fragments: base `docker-compose.yml` (just the `app` service) + optional `compose.{sqlserver,postgres,azurite}.yml`. The `post-create.sh` is parametrized by env vars on the `app` service (`DB_ENGINE`, `BACKEND_KIND`, `USE_AZURITE`, `USE_FRONTEND`) — it does NOT need editing to switch stack. See `template-backend/.devcontainer/README.md`. The repo root has no devcontainer (the framework itself is a pure .NET lib — no Functions/SQL/frontend).

[bootstrap/allowlist.md](bootstrap/allowlist.md) records what was imported from the legacy source repo and what must be adapted vs. never copied — useful when deciding whether a new artifact is generic enough to belong here.
