# Split the result core out of Limaj.Framework.Abstractions

**Status:** Done (implementation — functional sign-off is the user's decision)
**Última revisão:** 2026-10-04

## Context

This comes from the same consumer request that produced `web-error-extensibility.md`, where it
was listed as item **A3** and marked desirable, not blocking.

`Limaj.Framework.Abstractions` mixes two concerns today:

- **The result core:** `Result`/`Result<T>`, `Error`/`ErrorType`, the framework exceptions
  (`DomainValidationException`, `NotFoundException`, `ConflictException`) and
  `IExceptionToErrorMapper`.
- **The persistence abstractions:** `BaseEntity` (soft delete via `IsActive`), `IAudit`,
  `IRepository` and `IUnitOfWork`.

A consumer that only wants Result/Error gets the persistence model too. The consumer proposed
moving the result core into its own package, using `TypeForwardedTo` so existing consumers do
not break.

In the `/flow` session of 2026-10-04 this item was taken out of the error-extensibility point
by consensus: it is unrelated to the HTTP error contract, and mixing them would be scope creep.
It has **not** gone through a mediation round of its own yet.

## Initial positions (from that session, not decisions)

- **Architect.**
  - **Gain:** conceptual cohesion only. `Abstractions` has no third-party dependency, so the
    persistence types cost a result-only consumer nothing at runtime.
  - **Cost:** a new package in the lockstep versioning. `TypeForwardedTo` means keeping the
    `Limaj.Framework.Abstractions.*` namespace in a package with another name. The architecture
    test changes, because `Abstractions` would no longer have zero dependencies.
  - **Timing:** minor with forwarding, or a clean major with a new namespace. The 3.0.0 planning
    of `web-error-extensibility.md` is the natural window.
- **Analyst.** Accept the idea, but there is no proven pain yet: it was a "desirable" item and
  the current cost is conceptual pollution. With forwarding it would be minor.

## Architectural decisions

Settled in the `/flow` execution run of 2026-10-04 (1 round + 1 counter-argument; the
questions still diverging after it were decided provisionally by the architect, for the user to
confirm or revert on PR #1).

- **DA-001 — Split, in 3.0.0, as a clean break.** *(user, for doing it and the version —
  consensus, for no forwarding)*
  - The split is done, and ships in the 3.0.0 major with PR #1 (`web-error-extensibility`
    DA-013).
  - **No `TypeForwardedTo`:** forwarding keeps the full type name, so the new package would
    declare `Limaj.Framework.Abstractions.*` namespaces forever and Abstractions would have to
    reference it only to host the forwarders. In a major, consumers recompile anyway.

- **DA-002 — Package `Limaj.Framework.Core`, two namespaces, written charter.**
  *(provisional — decided by architect; other view: `Limaj.Framework.Results` with a single
  namespace, because "Core" attracts unrelated types; to revert: rename project/csproj/namespace
  to `Limaj.Framework.Results`, add `using HttpResults = Microsoft.AspNetCore.Http.Results;` in
  Web and rewrite the bare `Results.*` call sites there)* ADR candidate.
  - Package, assembly, folder and root namespace: `Limaj.Framework.Core`
    (`packages/Limaj.Framework.Core/`).
  - Namespaces map 1:1 to the old usings: `Limaj.Framework.Abstractions.Common` →
    `Limaj.Framework.Core`; `Limaj.Framework.Abstractions.Errors` →
    `Limaj.Framework.Core.Errors`.
  - **Why not `Results`:** a `Limaj.Framework.Results` namespace hides
    `Microsoft.AspNetCore.Http.Results` from all code under `namespace Limaj.Framework.*`
    (enclosing namespaces bind before using directives); `DefaultErrorHttpMapper` calls
    `Results.*` in six places, and every future framework host package would pay it.
  - **Charter**, in the csproj `Description`, `CLAUDE.md` and `GLOSSARY.md`:
    "dependency-free result/error contract only; any other type needs a DA".

- **DA-003 — What moves and what stays.** *(consensus)*
  - **To `Limaj.Framework.Core`:** `Result`, `Result<T>`, `Error`, `ErrorType`.
  - **To `Limaj.Framework.Core.Errors`:** `DomainValidationException`, `NotFoundException`,
    `ConflictException`, `IExceptionToErrorMapper` (its signature `Error? Map(Exception)` ties it
    to `Error`).
  - **Stays in Abstractions** (name unchanged): `BaseEntity`, `IAudit` (`.Domain`),
    `IRepository`, `IUnitOfWork` (`.Contracts`), `IUserIdentityGateway` (`.Identity`).
  - **Stays in Web:** `BuiltInExceptionToErrorMapper` and the rest of `web-error-extensibility`.
  - Files move with `git mv` so history is kept.

- **DA-004 — Dependency graph.** *(consensus, except Application's reference: provisional —
  decided by architect; other view: Application references only what it uses today, i.e.
  Abstractions; to revert: drop the `Core` ProjectReference from `Application.csproj` and add
  the matching CHANGELOG line for Application-only consumers)*
  - Core and Abstractions are **siblings**; neither references the other (nothing in
    Abstractions uses Result/Error).
  - **Actual references:** Application → Abstractions + Core (keeps `Result` reachable for a
    product that references only Application, as it was through Abstractions; `CLAUDE.md` names
    `Result` the application-layer contract). Persistence.EFCore → Abstractions + EF Core.
    Web → Core + `Microsoft.AspNetCore.App` (it uses nothing else from Abstractions).
  - **Allowed by the architecture test:** Core → nothing; Abstractions → nothing;
    Application → {Abstractions, Core}; Persistence.EFCore → {Abstractions, Core, EF Core};
    Web → {Abstractions, Core, ASP.NET Core}; never Application ↔ Persistence ↔ Web. Core is a
    bottom layer any package may reference. The tests anchor `Core` with `typeof(Result)` and
    `Abstractions` with `typeof(BaseEntity)`, and assert that Application's actual framework
    references are exactly {Abstractions, Core}.
  - `CLAUDE.md`'s layering table and "Core patterns" links are updated to match.

- **DA-005 — `web-error-extensibility` reconciliation goes in this commit.** *(consensus)*
  Apply `web-error-extensibility` DA-013's consequences here: this commit moves `Error.cs`, so
  its `[Obsolete]` messages are rewritten (3.0.0 → 4.0.0) where the type lands, together with
  every other "3.0.0"/"2.1.0" text listed there and the CHANGELOG reframing.

- **DA-006 — Tests, packaging and migration notes.** *(consensus)*
  - New `packages/Limaj.Framework.Core/Limaj.Framework.Core.csproj` with the Abstractions
    layout (`src/`, nested `Limaj.Framework.Core.Tests/`, `Compile Remove` block, MinVer 8.0.0,
    `MinVerTagPrefix v`, net10.0). Description: "Result/Error flow for Limaj Framework:
    Result/Result<T>, Error/ErrorType, framework exceptions and the IExceptionToErrorMapper
    extension point. Dependency-free result/error contract only; any other type needs a DA."
  - `git mv` `ResultTests`/`ErrorTests` into `Limaj.Framework.Core.Tests`; they must pass with
    only `using` changes (any other assertion change goes through test-failure-triage). All Web
    and Persistence tests pass with only using/reference changes, and HTTP output is identical.
  - Both new projects join `Limaj.Framework.sln`; `dotnet pack` produces 5 packages; the
    "4 packages" comments (`Directory.Build.props`, publish workflow) become 5.
  - Abstractions' `Description` drops Result/Error and the exceptions.
  - `CHANGELOG.md` "Upgrading from 2.x to 3.0.0": upgrade every `Limaj.Framework.*` package
    together (mixing 2.x and 3.x causes `TypeLoadException`); add `Limaj.Framework.Core` when
    you used Result/Error only through Abstractions or Persistence.EFCore; using mapping table;
    no wire change (bodies, `Error.Code`, statuses). README and `PACKAGE_README.md` list the
    new package and say it starts at 3.0.0 under lockstep versioning.

## Proposed structure by layer

```
packages/
  Limaj.Framework.Core/                         (new package — DA-002, DA-006)
    Limaj.Framework.Core.csproj                 (net10.0, MinVer 8.0.0, MinVerTagPrefix v, no references;
                                                 Description carries the charter)
    src/
      Result.cs                                 (git mv from Abstractions/src/Common — namespace Limaj.Framework.Core)
      Error.cs                                  (git mv — Error + ErrorType; HttpStatusCode stays [Obsolete],
                                                 message → "removed in 4.0.0" — DA-005)
      Errors/
        DomainValidationException.cs            (git mv from Abstractions/src/Errors — Limaj.Framework.Core.Errors)
        NotFoundException.cs                    (git mv)
        ConflictException.cs                    (git mv)
        IExceptionToErrorMapper.cs              (git mv)
    Limaj.Framework.Core.Tests/                 (new test project, nested like Abstractions.Tests)
      ResultTests.cs                            (git mv from Abstractions.Tests — only usings change)
      ErrorTests.cs                             (git mv — only usings change)
  Limaj.Framework.Abstractions/                 (keeps BaseEntity, IAudit, IRepository, IUnitOfWork,
                                                 IUserIdentityGateway — DA-003; Description trimmed — DA-006)
  Limaj.Framework.Application/                  (ProjectReference → Abstractions + Core — DA-004)
  Limaj.Framework.Persistence.EFCore/           (ProjectReference → Abstractions, unchanged — DA-004)
  Limaj.Framework.Web/                          (ProjectReference → Core only; usings → Limaj.Framework.Core[.Errors];
                                                 3.0.0 → 4.0.0 texts — DA-004, DA-005)
  Limaj.Framework.Architecture.Tests/           (Core anchored by typeof(Result), Abstractions by typeof(BaseEntity);
                                                 Application's framework references exactly {Abstractions, Core} — DA-004)
  Directory.Build.props                         ("4 packages" → 5 — DA-006)
  PACKAGE_README.md                             (lists Limaj.Framework.Core — DA-006)
.github/workflows/publish-packages.yml          ("4 pacotes" → 5 — DA-006)
Limaj.Framework.sln                             (+ Core and Core.Tests — DA-006)
CLAUDE.md, GLOSSARY.md, README.md, CHANGELOG.md (layering table, links, charter, upgrade notes — DA-002, DA-004, DA-005, DA-006)
docs/epics/in-progress/web-error-extensibility.md (DA-013 consequences — DA-005)
```

Core and Abstractions are sibling bottom layers that reference nothing; no host or
infrastructure type enters either, and the graph stays acyclic.

## Test strategy

- **Moved tests, unchanged assertions:** `ResultTests` and `ErrorTests` move with `git mv` to
  `Limaj.Framework.Core.Tests` and pass with only `using`/namespace changes (DA-006). Any other
  assertion change goes through `test-failure-triage`.
- **Behavior-neutral for every other suite:** the Application, Persistence.EFCore and Web test
  projects pass with only `using`/reference changes. The Web characterization and contract
  suites stay green unchanged, which shows the HTTP output (status, `Content-Type`, body,
  `Error.Code`) is identical — no wire change.
- **Architecture tests (DA-004):**
  - Core depends on no other framework package; Abstractions depends on no other framework
    package (Core included);
  - Application, Persistence.EFCore and Web never depend on one another;
  - Application's framework `ProjectReference`s are exactly {Abstractions, Core}, read from
    its `.csproj`: the compiler drops a reference to an assembly whose types are unused, so
    reflection over Application's metadata cannot see Core.
- **Packaging:** `dotnet pack Limaj.Framework.sln` produces 5 `.nupkg` files.
- **Text reconciliation (DA-005):** no "removed in 3.0.0", "2.1.0", `Abstractions.Common` or
  `Abstractions.Errors` left in `packages/`, `docs/epics/in-progress/`, `README.md` or
  `CHANGELOG.md`, except historical notes.

## Implementation notes

Choices made where the decisions above left the mechanics open:

- **Application's exact references:** the architecture test reads the `ProjectReference`s of
  `Limaj.Framework.Application.csproj` (repository root found by walking up to
  `Limaj.Framework.sln`), because Application uses no Core type yet and the compiler drops
  unused assembly references from its metadata.
- **`Limaj.Framework.Abstractions.Tests`:** both of its test files moved to Core, so it gained
  one test (`BaseEntityTests.NewEntity_IsActiveByDefault`, the soft-delete default) instead of
  staying as an empty project in the solution.
- **`FrameworkErrorTypesTests`:** `AbstractionsAssembly_…` was renamed
  `CoreAssembly_DefinesOnlyTheDocumentedClosedSetOfExceptionTypes`, since the assembly it
  inspects (via `typeof(IExceptionToErrorMapper)`) is now Core; the assertion is unchanged.
- **CHANGELOG `Error.HttpStatusCode` entry:** "There is no binary break" was dropped, because
  `Error` now changes assembly in the same release; the signature-compatibility part stays.
- **"No binary break" leftovers** (PR #1 review): the `Error` XML remarks and the ErrorTests
  test `HttpStatusCode_IsObsolete_WithoutBinaryBreak` (renamed
  `HttpStatusCode_IsObsolete_AndV2SignaturesAreKeptForSourceCompatibility`) now claim only
  source compatibility. Every assertion (signatures kept, obsolete markers) still holds for
  source compatibility, so none changed and no triage applied. The README install and release
  examples use 3.0.0, since `Limaj.Framework.Core` has no earlier version.

## Phase checklist

### Phase 1 — Decide
- [x] Mediate DA-001 in `/flow`, then fill in the structure, tests and implementation phases

### Phase 2 — `Limaj.Framework.Core` package (DA-002, DA-003, DA-006)
- [x] `Limaj.Framework.Core.csproj` (Abstractions layout, MinVer, net10.0, charter Description) and
      `Limaj.Framework.Core.Tests`; both added to `Limaj.Framework.sln`
- [x] `git mv` `Result.cs`/`Error.cs` to `Core/src/` (namespace `Limaj.Framework.Core`) and the
      3 exceptions + `IExceptionToErrorMapper` to `Core/src/Errors/` (`Limaj.Framework.Core.Errors`)
- [x] `git mv` `ResultTests`/`ErrorTests` to `Limaj.Framework.Core.Tests` (only usings change)
- [x] Abstractions keeps `BaseEntity`/`IAudit`/`IRepository`/`IUnitOfWork`/`IUserIdentityGateway`;
      its `Description` drops Result/Error and the exceptions

### Phase 3 — Dependency graph (DA-004)
- [x] References: Application → Abstractions + Core; Persistence.EFCore → Abstractions;
      Web → Core only; Web source and tests use `Limaj.Framework.Core[.Errors]`
- [x] Architecture tests: Core and Abstractions anchors, allowed sets, Application's framework
      references exactly {Abstractions, Core}

### Phase 4 — `web-error-extensibility` DA-013 reconciliation (DA-005)
- [x] Code, XML docs, `[Obsolete]` messages and test comments: removals/default flip → 4.0.0,
      first-shipping 2.1.0/2.x → 3.0.0/3.x
- [x] `packages/Limaj.Framework.Web/README.md`
- [x] `web-error-extensibility` epic: title, version labels, Phase 8 → "4.0.0" (plus removal of
      `IUserIdentityGateway`'s obsolete members), "Cut the 2.1.0 tag" → "Cut the 3.0.0 tag"

### Phase 5 — Documentation and packaging (DA-002, DA-004, DA-005, DA-006)
- [x] `CLAUDE.md` layering table, "Core patterns" links and charter; `GLOSSARY.md` charter
- [x] `README.md` and `PACKAGE_README.md` list `Limaj.Framework.Core` (starts at 3.0.0, lockstep);
      "4 packages" → 5 in `Directory.Build.props` and the publish workflow
- [x] `CHANGELOG.md` `[Unreleased]`: "Planned as 3.0.0, a major release", BREAKING entry for the
      split, "Upgrading from 2.x to 3.0.0" (lockstep upgrade, add `Limaj.Framework.Core`, using
      mapping table, no wire change); still no version heading or date
- [x] Validate: `dotnet build`, `dotnet test` green, `dotnet pack` produces 5 packages
