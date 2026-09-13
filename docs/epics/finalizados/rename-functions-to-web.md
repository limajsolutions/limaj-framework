# Rename Limaj.Framework.Functions to Limaj.Framework.Web

**Status:** Implementation done — pending business sign-off from the user
**Última revisão:** 2026-09-13

## Context

A technical investigation (`/spike`, investigation mode) compared `packages/Limaj.Framework.*`
with a real, independently built product (`FinanceFlow`), confirming that no `.csproj` in
FinanceFlow references `Limaj.Framework.*` — it's a parallel reimplementation. Among other
findings, that comparison revealed that `Limaj.Framework.Functions`
(`FunctionRunner`, `ResultExtensions`, `ExceptionExtensions`) **contains no line coupled to
Azure Functions** — it uses exclusively `Microsoft.AspNetCore.Http.HttpRequest`/`IResult`,
the same types ASP.NET Core's Minimal API uses natively. The package's current name
suggests a coupling that doesn't exist in the code.

`/arquiteto` and `/analyst` were consulted via `/flow` about a broader proposal (rename +
generalize for multiple HTTP hosts + embed vertical slicing via a base class). After
independent opinions and one round of counter-argument, they reached **partial** consensus:
the rename is approved and makes sense on its own (see DA-001); generalizing to support
Minimal API as a second host requires no new work (the contract is already agnostic); and
embedding vertical slicing via `IEndpoint`/`MapEndpoints` was explicitly **excluded from
this epic's scope** (see DA-003 — a closed decision to not do it now, with a defined
trigger to reopen it).

## Architectural decisions

- **DA-001 — Rename, not a package split.** `Limaj.Framework.Functions` becomes
  `Limaj.Framework.Web`, keeping the same dependency (`Abstractions` +
  `FrameworkReference Microsoft.AspNetCore.App`). There is no Azure-Functions-specific code
  today to extract as a separate adapter — trigger binding (`[Function("X")]`) always was,
  and remains, product code, not framework code. A separate adapter package
  (`Limaj.Framework.Web.AzureFunctions`) is only justified if some product needs the
  Worker's non-ASP.NET-Core binding (`HttpRequestData`/`HttpResponseData`) — with no
  evidence of that today, it will not be implemented preemptively.

- **DA-002 — Host-neutral vocabulary.** Parameters/messages that currently use Azure
  Functions vocabulary (`functionName`, `"in {Function}"` in logs) switch to host-agnostic
  vocabulary (`operationName` or equivalent), since the package is intended to be usable by
  any host that speaks `HttpRequest`/`IResult`.

- **DA-003 — Vertical slicing (`IEndpoint`/`MapEndpoints`) stays out of this epic.**
  After one round of counter-argument, the architect and analyst converged on
  **specifying the design, but not implementing it in `packages/` yet**: the proposed
  contract (`void MapEndpoint(IEndpointRouteBuilder app)`, registered via
  `MapEndpoints(assembly)` restricted to an explicit assembly, never a global scan) meets
  the requirements of not assuming "1 endpoint = 1 domain resource" and of having an escape
  hatch (whoever doesn't use Minimal API never references the type). Even so, it stays out
  because **no real product uses Minimal API today** — publishing this public API without a
  real consumer to validate the contract's shape is exactly the kind of speculative
  generalization `CLAUDE.md` asks to avoid in `packages/Limaj.Framework.*`. Open point for
  when this is revisited: where `IEndpoint` should physically live so it doesn't pollute
  the public surface for whoever only uses Azure Functions (an isolated namespace/package
  within `Web`). **Reopening trigger:** the first real product that decides to expose
  endpoints via Minimal API — at that point, extract the already-validated contract against
  the real cases that show up (bulk, multi-aggregate webhook, cross-domain report), instead
  of designing it blindly now.

- **DA-004 — Collateral security fix, in the same PR.** `ExceptionExtensions`/
  `ResultExtensions.MapError`, on the unexpected-error branch (500), currently return
  `ex.Message` raw in the HTTP response body (`Results.Problem(title: error.Message, ...)`)
  — leaking internal detail (EF Core messages, third-party stack traces) to the client. Fix:
  log the full `ex.Message` via `ILogger` (already does this today), but return a fixed
  generic message to the client ("An unexpected error occurred.") whenever not in a
  development environment.

## Proposed structure by layer

```
packages/Limaj.Framework.Web/                    (rename of Limaj.Framework.Functions)
  Limaj.Framework.Web.csproj                      (same deps: Abstractions + FrameworkReference AspNetCore.App)
  src/
    Http/
      RequestRunner.cs        (formerly FunctionRunner.cs; parameter "functionName" -> "operationName")
      ResultExtensions.cs      (logic unchanged; vocabulary adjustment only where "function" is referenced)
      ExceptionExtensions.cs   (vocabulary adjustment + DA-004 fix)
```

Impact on other files in the repo (outside `packages/`, but part of the same delivery):
- `CLAUDE.md` — layer table: `Limaj.Framework.Functions` → `Limaj.Framework.Web`
  (allowed dependencies unchanged); the "Exception bridge" section updates type/file names.
- `README.md` — the repository-structure item describing `Limaj.Framework.Functions`.
- Any existing consumer of the package (today, potentially `FinanceFlow` or
  `template-backend`, if already copied) needs to update its `PackageReference`/`ProjectReference`
  and `using Limaj.Framework.Functions.Http` → `Limaj.Framework.Web.Http`.

## Test strategy

> **Execution note (2026-09-13):** the text below, as originally written, assumed this epic
> would create "the repository's first test project". That stopped being true between the
> epic's formalization (2026-09-12) and its implementation:
> `test-foundation-and-persistence-error-fixes.md` (completed in `docs/epics/finalizados/`)
> already created `Limaj.Framework.Functions.Tests` covering exactly the two points below —
> the 7 `ErrorType` combinations in `ResultExtensionsTests.cs` and the 4 exception-bridge
> branches in `ExceptionBridgeTests.cs`, already passing against the pre-rename code. The
> original Phase 1 (create the project + characterization tests) was therefore absorbed:
> instead of recreating it, the existing test project was renamed together with the package
> in Phase 2 (`Limaj.Framework.Functions.Tests` → `Limaj.Framework.Web.Tests`), preserving
> the already-existing characterization tests as the rename's safety net. Decision made with
> the user in this session before proceeding.

1. ~~Create `Limaj.Framework.Web.Tests`~~ — already existed as `Limaj.Framework.Functions.Tests`
   (see note above), covering:
   - `ResultExtensions.MapError` — the 7 `ErrorType` → status code/body combinations.
   - `ExceptionExtensions.ToHttpResult` — the 4 branches (`DomainValidationException`,
     `NotFoundException`, `ConflictException`, default), including what gets logged.
2. Tests confirmed passing **before** the physical rename (inherited from the test-foundation epic).
3. Physical rename applied (file-level `git mv` — see execution note in Phase 2 —,
   namespace, `.csproj`) + vocabulary adjustment (DA-002).
4. The same tests, renamed along with the project, pass **after** the rename with no
   assertion changes — proof of a pure rename, not a disguised rewrite.
5. A specific test added for the security fix (DA-004): an unmapped error returns a
   generic message in the body outside a development environment; the original
   `ex.Message` only appears in the log (`ExceptionBridgeTests.ToHttpResult_UnknownException_*`).

## Phase checklist

### Phase 1 — Safety net before the rename
- [x] ~~Create the `Limaj.Framework.Web.Tests` project~~ — already existed (`Limaj.Framework.Functions.Tests`,
      created by `test-foundation-and-persistence-error-fixes.md`); renamed in Phase 2 instead of
      recreated. See execution note above.
- [x] Characterization tests for `ResultExtensions.MapError` (7 `ErrorType`) — already existed
- [x] Characterization tests for `ExceptionExtensions.ToHttpResult` (4 branches) — already existed
- [x] Confirm all tests pass against the current code (`Limaj.Framework.Functions`) —
      confirmed via `dotnet test` before the physical rename

### Phase 2 — Physical rename
- [x] Package renamed to `packages/Limaj.Framework.Web` preserving history (file-level rename
      via `git mv`/`git add`, not directory-level — a background process lock from VS
      Code/OmniSharp prevented `git mv`/`Rename-Item` on the whole directory; git detected the
      rename anyway by each file's content similarity)
- [x] `.csproj` (project/assembly name), namespaces, and references in `Limaj.Framework.sln`
      updated; `Limaj.Framework.Architecture.Tests` also adjusted (it referenced the package)
- [x] Phase 1's characterization tests keep passing with no assertion changes —
      confirmed via `dotnet test` (28/28 in `Limaj.Framework.Web.Tests`, 4/4 in
      `Limaj.Framework.Architecture.Tests`)

### Phase 3 — Vocabulary adjustment and security fix
- [x] `FunctionRunner` → `RequestRunner`; parameter `functionName` → `operationName` (DA-002)
- [x] `ex.Message` leak on the 500 branch fixed (DA-004): generic message to the client outside
      `ASPNETCORE_ENVIRONMENT=Development`, the full `ex.Message` stays only in the log +
      tests covering both branches (`ExceptionExtensions.cs`, `ExceptionBridgeTests.cs`)
- [x] Log messages/names that referenced "function" updated to host-neutral vocabulary
      (`{Operation}`)

### Phase 4 — Documentation and consumer updates
- [x] Layer table and "Exception bridge" section in `CLAUDE.md` updated
- [x] `README.md` (repository structure, architectural boundaries, building blocks) updated
- [x] Consumer survey: no real product references `Limaj.Framework.Functions` via
      `ProjectReference` or a published NuGet package today (confirmed by a text search across
      the repo before the rename — the same finding already recorded in DA-005 below) —
      **no compatibility shim needed**, a condition already anticipated in DA-005

## Open decisions

None. DA-005 was closed below — the missing information (how the package is distributed
today) has been gathered.

## Architectural decisions (update — `/flow` session, 2026-09-12)

- **DA-005 — No compatibility shim, conditioned on execution order.** A direct survey
  (no `PackageId`/`GeneratePackageOnBuild` in any `.csproj`, no `nuget.config`, no publish
  workflow at the repo root) confirmed that **no distribution mechanism exists today** for
  `Limaj.Framework.Functions` — no published NuGet package, no formal `ProjectReference` by
  any real product (`FinanceFlow` reimplemented everything in parallel). There's no
  consumer to protect, so **no shim is needed**. This is conditioned on a specific order,
  closed together with `/arquiteto` and `/analyst` in the session that formalized
  `docs/epics/backlog/nuget-package-publishing-pipeline.md`: **this epic (rename) must be
  completed before the first real package publish.** If that order is respected,
  `Limaj.Framework.Functions` never exists as a published package — there's nothing for a
  shim to protect. If the order is inverted (publish happens before the rename), the answer
  changes: it would require a deprecation-cycle shim, because at that point an
  already-published contract would break.

## Related

- Technical investigation that grounded this epic: `Limaj.Framework.*` vs.
  `FinanceFlow` comparison (`/flow` session, 2026-09-12), which also identified other gaps
  (extensibility of `ErrorType`/exceptions, persistence, identity, test foundation) — now
  formalized in `docs/epics/backlog/test-foundation-and-persistence-error-fixes.md`.
- DA-003 (vertical slicing via `IEndpoint`/`MapEndpoints`) is recorded here as a decision
  to not do it now, not as a work item — it does not become its own epic until the
  described trigger fires.
- `docs/epics/backlog/nuget-package-publishing-pipeline.md` — the package-publishing epic,
  whose Phase 2 depends on this epic (rename) being complete before the first real publish.
