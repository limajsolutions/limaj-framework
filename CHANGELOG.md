# Changelog

All notable changes to this framework are recorded here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Entries are organized by
version. The `[2026-06-06]` section predates versioning and keeps its date heading.

## [Unreleased]

Planned as 3.0.0, a major release (epics `web-error-extensibility`, `result-core-package-split`,
`typed-user-principal`; `web-error-extensibility` DA-013). 2.1.0 is never released: its changes ship here. There is no
`v3.0.0` tag yet, so these notes carry no version heading and no release date. The BREAKING
entries below need source changes; read "Upgrading from 2.x to 3.0.0" first. The rest is
additive or opt-in, except the "Security" fixes, which ship on by default as an explicit
exception (DA-005). Everything marked `[Obsolete]` here is removed in the next major, planned
as 4.0.0.

### Upgrading from 2.x to 3.0.0

- **Upgrade every `Limaj.Framework.*` package together.** `Result`, `Error` and the framework
  exceptions moved to a new assembly, with no type forwarding: mixing 2.x and 3.x packages in
  one application fails at run time with `TypeLoadException`.
- **Add `Limaj.Framework.Core`** if you used `Result`/`Error` or the framework exceptions only
  through `Limaj.Framework.Abstractions` or `Limaj.Framework.Persistence.EFCore`. Projects that
  reference `Limaj.Framework.Application` or `Limaj.Framework.Web` get it transitively.
- **Replace the usings** (the type names do not change):

  | 2.x | 3.0.0 |
  |---|---|
  | `using Limaj.Framework.Abstractions.Common;` (`Result`, `Result<T>`, `Error`, `ErrorType`) | `using Limaj.Framework.Core;` |
  | `using Limaj.Framework.Abstractions.Errors;` (`DomainValidationException`, `NotFoundException`, `ConflictException`, `IExceptionToErrorMapper`) | `using Limaj.Framework.Core.Errors;` |

  `Limaj.Framework.Abstractions.Domain`, `.Contracts` and `.Identity` keep their names.
- **No wire change:** response bodies, `Error.Code` values and HTTP statuses are the same as
  2.x for the same configuration.
- **`IUserIdentityGateway` implementers:** implement `GetCurrentPrincipalAsync` (or
  `IUserIdentityGateway<TPrincipal>`) and **delete your own `GetCurrentUserIdAsync` /
  `IsAuthenticatedAsync` implementations** — they would silently override the new delegating
  defaults, with no compiler warning. Move callers to the `GetCurrentPrincipalAsync` snapshot
  (see the BREAKING entry below).
- **`IUserIdentityGateway` callers:** calls to `GetCurrentUserIdAsync` / `IsAuthenticatedAsync`
  through the `IUserIdentityGateway` interface keep compiling, with warning CS0618. Calls on a
  variable typed as the **concrete implementing class** fail with error **CS1061**: a default
  interface member is reachable only through the interface. Call through the interface, or move
  to `GetCurrentPrincipalAsync`.

### Changed
- **BREAKING — the result core moved to the new `Limaj.Framework.Core` package**
  (`result-core-package-split`, DA-001…DA-006). `Result`, `Result<T>`, `Error` and `ErrorType`
  are now in namespace `Limaj.Framework.Core`; `DomainValidationException`,
  `NotFoundException`, `ConflictException` and `IExceptionToErrorMapper` in
  `Limaj.Framework.Core.Errors`. No `TypeForwardedTo`: this is a clean break in a major.
  `Limaj.Framework.Core` is dependency-free, ships in lockstep with the other packages and
  starts at 3.0.0. `Limaj.Framework.Abstractions` keeps `BaseEntity`, `IAudit`, `IRepository`,
  `IUnitOfWork` and `IUserIdentityGateway` and no longer contains the result types. Package
  references: `Application` → `Abstractions` + `Core`; `Persistence.EFCore` → `Abstractions`;
  `Web` → `Core` only.
- **BREAKING — `IUserIdentityGateway` returns a typed, extensible principal**
  (`typed-user-principal`, DA-001…DA-007; `Limaj.Framework.Abstractions.Identity`).
  - New abstract member `Task<UserPrincipal?> GetCurrentPrincipalAsync(CancellationToken = default)`:
    one snapshot of the caller, so the user id and the authenticated state can no longer
    disagree. `null` = no authenticated caller; a non-null principal = authenticated, with
    `UserId == null` for a non-user caller (service-to-service, client credentials). Every
    implementer must implement it.
  - New `UserPrincipal` base class, which products extend (e.g. with a `TenantId`): validating
    constructor (`ArgumentException` for an empty/whitespace id, without echoing it), get-only
    `UserId` (the identity provider's opaque, stable id — personal data), `HasUserId`, and a
    sealed, redacting `ToString()` (`{TypeName} { [redacted] }`).
  - New `IUserIdentityGateway<TPrincipal> : IUserIdentityGateway` returning the product's
    principal; it serves the non-generic member with the same instance.
  - No host adapter ships: map your identity provider's claims to a principal in your host
    (recipe and privacy rules in the Web README). No identity contract exposes
    `System.Security.Claims` or ASP.NET Core types.
  - **Migration:** implement `GetCurrentPrincipalAsync` (or the generic interface); **delete
    your own `GetCurrentUserIdAsync` / `IsAuthenticatedAsync` implementations**, which would
    silently override the delegating defaults with no compiler warning; move callers to the
    snapshot (`principal is null` / `principal.UserId`). Callers through the interface get
    warning CS0618; callers typed as the concrete gateway class get error CS1061 (see
    "Upgrading from 2.x to 3.0.0").
- **Log levels and EventIds of the exception bridge** (DA-006). An exception mapped by the host's `IExceptionToErrorMapper` is now logged by its resulting status: 5xx at `Error` (EventId 5000), 4xx at `Warning` (4003). It used to be `Warning` regardless, so **new error-level alerts may fire**. Every event now has a stable `EventId` via `LoggerMessage` (table in the Web README). The unhandled-exception log now also carries `ex.Source`.

### Added
- **`IErrorHttpMapper` extension point** (`Limaj.Framework.Web`, DA-002): the single place that writes every error response. `DefaultErrorHttpMapper` is public, with one virtual method per `ErrorType` plus `MapWithStatusCode(error, statusCode)`. A product inherits or decorates it, for example to answer 422 keyed on `Error.Code`.
- **`IHttpResultResponder`**, the injected entry point (`ToHttpResult` + `RunAsync`), and **`AddLimajHttpErrors(...)`** with `LimajHttpErrorOptions` (DA-002). Exceptions go through the same `IErrorHttpMapper` as failed results. `AddLimajHttpErrors` registers no `IProblemDetailsWriter` and does not call `AddProblemDetails`. The static facades (`ResultExtensions`, `RequestRunner`, `ExceptionExtensions`) keep their signatures and concrete result types, and now delegate to `DefaultErrorHttpMapper` in `V2`.
- **`LimajProblemDetailsFormat { V2, V3 }`**, default `V2` (DA-003). `V3` adds `code` to every error response (validation included), writes 404/409 through `IProblemDetailsService`, and sets `detail = Error.Message` with `title` by status.
- **`LimajHttpErrorOptions.IncludeExceptionDetails`** (`bool?`, DA-004). `null` keeps the `ASPNETCORE_ENVIRONMENT` read; `true`/`false` always win; `true` logs a fixed warning at startup. Only `Exception.Message` is ever exposed.
- **`BuiltInExceptionToErrorMapper`**: the built-in exception bridge as a public `IExceptionToErrorMapper` (DA-006). It still runs before the host's mapper.
- **`Error.RetryAfter`** (`TimeSpan?`, `init`, outside the constructor, DA-007). The default mapping emits a `Retry-After` header in whole seconds, rounded up, via `RetryAfterHttpResult`, plus `retryAfter` in the `V3` body. The README documents the `RateLimiterOptions.OnRejected` recipe for the ASP.NET Core rate limiter.
- **`LimajHttpErrorOptions.IncludeDetailsOutsideValidation`** (DA-008): opt-in to send `Error.Details` as `details` for types other than Validation. Never on a 5xx.

### Deprecated
- **`Error.HttpStatusCode`** (DA-010): `[Obsolete]`, removed in 4.0.0. Map statuses outside the `ErrorType` table in your `IErrorHttpMapper`, keyed on `Error.Code` (`MapWithStatusCode`). `Error` is now an explicit record that keeps the 2.0 five-parameter constructor and five-value `Deconstruct` with the same signatures (both `[Obsolete]`), so code that sets `HttpStatusCode` still compiles (source compatibility, once its usings point to `Limaj.Framework.Core`), with a warning. It is not binary compatible: `Error` changed assembly, so recompile. `[OverloadResolutionPriority]` keeps calls without `HttpStatusCode` on the new four-parameter constructor.
- **`LimajHttpErrorOptions.ExposeUnexpectedResultMessage`**: born `[Obsolete]` as the temporary opt-out of the fix below, removed in 4.0.0.
- **`IUserIdentityGateway.GetCurrentUserIdAsync` and `IsAuthenticatedAsync`** (`typed-user-principal` DA-006): `[Obsolete]` default interface members, removed in 4.0.0. They now derive from `GetCurrentPrincipalAsync` (`principal?.UserId`, `principal is not null`). Callers through the `IUserIdentityGateway` interface keep compiling, with warning CS0618; callers holding the concrete implementing class get error CS1061, because a default interface member is reachable only through the interface. Use `GetCurrentPrincipalAsync()` instead.

### Security
- **The 500 for an unhandled exception no longer sends `ex.Source`** (DA-005, CWE-209). Its `detail` (V2) / `code` (V3) is now the fixed `unexpected_error`. `ex.Source` stays in the log.
- **A 5xx `ErrorType.Unexpected` result now sends `"An unexpected error occurred."` outside Development** (or with `IncludeExceptionDetails = false`), instead of its own message, which could carry SQL, configuration or personal data. Temporary opt-out: `ExposeUnexpectedResultMessage`. An `Unexpected` error answered with an explicit 4xx `HttpStatusCode` keeps its message.
- **A Validation error with `HttpStatusCode` set no longer drops its `errors`.**

## [2.0.0] - 2026-10-04

### Removed
- **Claude Code commands, agents, and skills moved out of this repository.** Removed `.claude/commands/` (root mirror), `template-backend/.claude/commands/` (canonical copy), and `template-backend/scripts/sync-commands.sh` (no longer has anything to sync). They now live in [thalleslima8/my-skills](https://github.com/thalleslima8/my-skills) as the `workflow` plugin; the generic `.claude/settings.json` + `CLAUDE.md` starting point lives in [thalleslima8/ai-starter-kit](https://github.com/thalleslima8/ai-starter-kit).

### Changed
- **BREAKING — the 4 `Limaj.Framework.*` packages now target `net10.0` only** (previously `net9.0`), with EF Core / `Microsoft.Extensions.Configuration*` dependencies on `10.0.12`; the test projects and the publish workflow (`dotnet-version: 10.0.x`) moved too. Consumers must be on .NET 10 — release this as a new major version. No source changes were needed.
- Root `.claude/settings.json` (same content as ai-starter-kit) now declares the `my-skills` marketplace and enables `workflow@my-skills`, so the commands remain usable when working on the framework itself.
- `template-backend/.devcontainer/claude-settings.json` and `post-create.sh` now state explicitly that they cover permissions only; marketplace/plugins come from the project's `.claude/settings.json` (ai-starter-kit + my-skills).
- README "Starting a new SaaS" guide: new step 6 (bring in `.claude/` + `CLAUDE.md` from ai-starter-kit) and step 8 (install `workflow@my-skills`, plus `dotnet@my-skills` for EF Core). Removed the "Keeping the commands in sync" section.

### Added
- **Stable releases are now published to nuget.org** (public, no authentication to consume), via a new `publish-nuget-org` job in `publish-packages.yml` that runs only on `vX.Y.Z` tags and authenticates with nuget.org Trusted Publishing (GitHub OIDC → short-lived API key, environment `nuget`). Pre-releases from `main` stay on GitHub Packages only. Reverses DA-001's exclusion of nuget.org.
- Package metadata for the 4 `Limaj.Framework.*` packages (`packages/Directory.Build.props`): author `Limaj Solutions`, MIT license, repository/project URLs, tags, a package README (`packages/PACKAGE_README.md`), symbol packages (`.snupkg`) and deterministic CI builds; a per-package `Description` in each `.csproj`.
- `LICENSE` (MIT).

### Fixed
- README consumption guide asked for a fine-grained PAT, which GitHub Packages' NuGet registry does not accept — it now asks for a classic PAT with `read:packages`, configured once per machine.

## [1.0.0] - 2026-09-13

### Changed
- **Consolidated slash commands from 10 to 6**, each with an exclusive, non-overlapping responsibility: `/analyst`, `/arquiteto`, `/flow`, `/spike`, `/qa`, `/infra`.
  - `/spike` absorbed `/dev` (implementation), `/bugfix` (fixing), and `/review` (code review) — it is now the only agent authorized to touch product code, operating in modes (investigation, epic implementation, direct implementation, bug fix, review).
  - `/flow` stopped being the orchestrator of the full cycle (`analyst → spike → SM → dev → bugfix`) and became exclusively the mediator between `/arquiteto` and `/analyst`: independent opinions, cross-check, at most 1 round of counter-argument, escalation to the user if the conflict persists.
  - `/infra` explicitly became the owner of everything under `.claude/` (commands, settings, canonical→root sync), plus the dev environment and automation.
  - `/qa` explicitly became independent, oriented by epics/documentation/code, with full autonomy over the local database's test data (never production).
  - `/arquiteto` gained a dedicated security/DevSecOps section (AuthN/AuthZ, sensitive data, attack surface, CI/CD and supply chain, retention/privacy).
  - `/analyst` gained a compliance/legal posture (privacy, data retention, legal exposure) as part of its critical checklist.
- `/SM`, `/dev`, `/bugfix`, and `/review` were **removed** from `.claude/commands/` (canonical in `template-backend/`, mirrored at the root via `sync-commands.sh`). *(Historical: these files have since moved to my-skills — see "Removed" under 2.0.0 above.)*

### Added
- Work-management convention via `docs/epics/{backlog,in-progress,done}/` replacing GitHub Issues/Projects as the tracking system — documented in [`docs/template-usage.md`](docs/template-usage.md) and referenced in `CLAUDE.md`.
- `template-backend/docs/README.md` and the `docs/epics/` folder skeleton — now ship ready-made with every new product created from the template.

## [2026-06-06] — Isolated Dev Container + initial slash commands

### Added
- Parametrizable Dev Container for `template-backend/` (Docker socket not mounted, broad allowlist isolated in `claude-settings.json`), with swappable stacks via `docker-compose` fragments (SQL Server, PostgreSQL, Azurite) and an idempotent `post-create.sh`.
- First batch of slash commands under `.claude/commands/`: `/analyst`, `/arquiteto`, `/spike`, `/SM`, `/dev`, `/bugfix`, `/review`, `/qa`, `/infra`, `/flow`. *(Historical: since moved to my-skills — see "Removed" under 2.0.0 above.)*
- `template-backend/scripts/sync-commands.sh` to keep the canonical copy (`template-backend/.claude/commands/`) and the root mirror (`.claude/commands/`) in sync. *(Historical: removed together with the copies it synced — see "Removed" under 2.0.0 above.)*
- Frontend CI and quality checks as placeholders (`echo "TODO: ..."`) under `template-backend/.github/workflows/`.
- Initial READMEs describing the purpose of each template component.
