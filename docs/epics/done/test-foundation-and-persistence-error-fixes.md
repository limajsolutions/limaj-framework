# Test foundation, UnitOfWork fix, and persistence/error extensibility

**Status:** Done (implementation — functional sign-off is the user's decision)
**Última revisão:** 2026-09-12

## Context

A technical investigation (`/spike`, investigation mode) comparing `packages/Limaj.Framework.*`
with a real, independently built product (`FinanceFlow`) revealed that the framework has no
test net today (`Limaj.Framework.sln` contains no test project) and carries a known
persistence-infrastructure bug, plus two additive extensibility points that FinanceFlow had
to reimplement in parallel due to their absence in the framework.

`/arquiteto` and `/analyst`, consulted via `/flow`, converged without needing a
counter-argument round on the four items below. Later, in a follow-up session about
creating a package-publishing pipeline (see Related), both specialists converged again on
**the lack of tests being blocking for publishing any versioned package** — publishing
without tests distributes as "verified" something that was only compiled. This makes this
epic's Phase 2 (test foundation) a direct prerequisite of the publishing epic, not just an
isolated nice-to-have quality item.

## Architectural decisions

- **DA-001 — `UnitOfWork<TDbContext>` bug (P0, pure fix).**
  `ExecuteInTransactionAsync` is incompatible with `EnableRetryOnFailure`: EF Core requires
  any code running inside a retry-enabled execution strategy to run via
  `Database.CreateExecutionStrategy().ExecuteAsync(...)`, and the current method doesn't do
  that — it opens a transaction directly, which fails (or masks a retry failure) when the
  `DbContext` is configured with retry enabled. There's no pending design decision here,
  it's a bug fix.

- **DA-002 — Additive persistence utilities (P1).** Three new extension methods, with no
  change to any existing contract: `DesignTimeDbContextFactoryBase` (a base for
  `IDesignTimeDbContextFactory<T>` used by migrations), `UseSqlServerWithRetry` (a wrapper
  around `UseSqlServer` that already configures `EnableRetryOnFailure` with the correct
  parameters, avoiding a recurrence of the DA-001 bug for whoever configures retry
  manually), and `UseUtcDateTimeConversion` (a value converter to force `DateTime`/
  `DateTimeOffset` as UTC in the EF Core mapping).

- **DA-003 — Error extensibility mechanism (P1).** `Error` gains an optional (nullable)
  `HttpStatusCode`, and a pluggable `IExceptionToErrorMapper` contract now exists that the
  host (`Limaj.Framework.Functions`/`Web`) can resolve via DI to map exceptions not covered
  by the default bridge. **`ErrorType` remains closed at 7 values — it does not gain new
  members.** Critical, no-exceptions constraint: no concrete product/domain error type
  (e.g. `PlanLimitExceeded`) can climb into the framework — only the extension mechanism
  can. A test must lock this in (see Phase 4).

- **DA-004 — Test foundation (blocking prerequisite for package publishing).** One test
  project per package (`Limaj.Framework.Abstractions.Tests`,
  `Limaj.Framework.Application.Tests`, `Limaj.Framework.Persistence.EFCore.Tests`,
  `Limaj.Framework.Functions.Tests`), added to `Limaj.Framework.sln`, covering the core
  contracts `CLAUDE.md` already names as reusable building blocks — exactly the pieces a
  (future) external consumer will depend on not silently breaking.

- **DA-005 — Dependency-table architecture test.** A reflection-based test (e.g.
  NetArchTest) that programmatically asserts the dependency direction between the 4
  packages (`Abstractions` references nothing; `Application`/`Persistence.EFCore`/
  `Functions` reference only `Abstractions` + their declared external dependencies from
  `CLAUDE.md`). The build already catches this via project references, but the test
  resists a future workaround via reflection/dynamic types that the build wouldn't catch.

## Proposed structure by layer

```
packages/
  Limaj.Framework.Abstractions/
    Limaj.Framework.Abstractions.Tests/        (new)
  Limaj.Framework.Application/
    Limaj.Framework.Application.Tests/          (new)
  Limaj.Framework.Persistence.EFCore/
    src/Persistence/Extensions/
      DesignTimeDbContextFactoryBase.cs          (new)
      SqlServerRetryExtensions.cs                (new — UseSqlServerWithRetry)
      UtcDateTimeConversionExtensions.cs          (new — UseUtcDateTimeConversion)
    Limaj.Framework.Persistence.EFCore.Tests/    (new)
  Limaj.Framework.Functions/
    Limaj.Framework.Functions.Tests/             (new)
  Limaj.Framework.Architecture.Tests/             (new — cross-package dependency test, DA-005)
```

`Limaj.Framework.sln` now includes 5 new test projects, keeping the same dependency
direction already documented in `CLAUDE.md` (no test project of one layer references
another layer's implementation beyond what that layer would already reference in
production).

## Test strategy

- **`Limaj.Framework.Abstractions.Tests`** — `Result`/`Result<T>` (every success/error
  combination), `Error` (including the new optional `HttpStatusCode` from DA-003).
- **`Limaj.Framework.Application.Tests`** — no domain logic to test today (the package is
  intentionally free of business rules); covers only whatever contracts/abstractions exist
  in the layer, if any.
- **`Limaj.Framework.Persistence.EFCore.Tests`** — `BaseRepository` (soft delete via
  `IsActive`, opt-in `includeInactive`, `HardDeleteByIdAsync`), `UnitOfWork<TDbContext>`
  (commit/rollback, and the specific DA-001 bug scenario: a transaction with retry enabled
  doesn't fail), the 3 new DA-002 utilities.
- **`Limaj.Framework.Functions.Tests`** — `ResultExtensions.ToHttpResult` (the 7
  `ErrorType` → status/body combinations), `ExceptionExtensions`/`FunctionRunner.RunAsync`
  (the 4 branches: `DomainValidationException`, `NotFoundException`, `ConflictException`,
  default), and the DA-003 pluggable `IExceptionToErrorMapper` (including the negative
  test: no product/domain error type is referenced in the framework assembly).
- **`Limaj.Framework.Architecture.Tests`** — only the DA-005 dependency test.

No coverage-percentage gate (`coverlet`/threshold) — behavioral coverage of the public
contracts is the criterion, not an arbitrary metric.

## Phase checklist

### Phase 1 — Bug fix (P0)
- [x] Fix `UnitOfWork<TDbContext>.ExecuteInTransactionAsync` to use
      `Database.CreateExecutionStrategy().ExecuteAsync(...)`
- [x] Characterization test covering a transaction with `EnableRetryOnFailure` enabled

### Phase 2 — Test foundation (blocking for the publishing epic, see Related)
- [x] Create the 4 test projects (one per package) + `Limaj.Framework.Architecture.Tests`,
      add all of them to `Limaj.Framework.sln`
- [x] Coverage of `Result`/`Error`/`ResultExtensions.ToHttpResult` (7 `ErrorType`)
- [x] Coverage of the exception bridge (`FunctionRunner.RunAsync` + `ExceptionExtensions`, 4 branches)
- [x] Coverage of `BaseRepository` (soft delete, `includeInactive`, `HardDeleteByIdAsync`) —
      **out-of-original-scope finding, fixed in this session with explicit user approval**:
      `BaseEntityConfiguration<T>.Configure()` never called `HasQueryFilter(e => e.IsActive)`,
      so soft delete excluded nothing by default and `includeInactive`
      (`IgnoreQueryFilters()`) was a no-op (there was no filter to ignore). Fixed by adding
      the `HasQueryFilter`. As a direct consequence of that fix,
      `SoftDeleteByIdAsync`/`RestoreByIdAsync` (which queried via `Set.Where(...)`, now
      subject to the filter) and `HardDeleteByIdAsync` (which delegated to the now-filtered
      `GetByIdAsync`, and used `AsNoTracking()` causing an identity conflict when the entity
      was already tracked in the same `DbContext`) also needed adjustment — all three now use
      `IgnoreQueryFilters()`, and `HardDeleteByIdAsync` stopped using `AsNoTracking()` to allow
      EF Core identity resolution. Covered by 9 new tests in `BaseRepositoryTests`.
- [x] Coverage of `UnitOfWork` (commit/rollback) — covered by the 3 Phase 1 characterization
      tests (`UnitOfWorkExecuteInTransactionAsyncTests`)
- [x] Architecture test validating the dependency table across the 4 packages

### Phase 3 — Persistence utilities (P1)
- [x] `DesignTimeDbContextFactoryBase`
- [x] `UseSqlServerWithRetry`
- [x] `UseUtcDateTimeConversion`
- [x] Tests covering the 3 utilities

### Phase 4 — Error extensibility mechanism (P1)
- [x] Optional `Error.HttpStatusCode`
- [x] Pluggable `IExceptionToErrorMapper`, resolved via DI in the host
- [x] Test guaranteeing `ErrorType` stays closed at 7 values
- [x] Negative test guaranteeing no concrete product/domain error type is referenced inside
      the framework

## Open decisions

> Pending decision: DA-006 — whether the `Limaj.Framework.Testing` package
> (+ `Testing.Functions`), raised in the same original investigation as P1 (AutoFixture+AutoMoq
> `TestBase`, specimen builders, a fake `IUserIdentityGateway`, a SQLite in-memory
> integration host), belongs in this epic or becomes its own epic. By the user's decision in
> this session, it was **not** included here — its scope is different (creating a new
> test-utilities package *for consumers*, not the framework's own test net) and it hasn't
> been formalized yet.

## Related

- **Blocks** `docs/epics/backlog/nuget-package-publishing-pipeline.md` — this epic's
  Phase 2 (test foundation) must be complete before the first real package publish.
- Technical investigation that surfaced the gaps: `Limaj.Framework.*` vs. `FinanceFlow`
  comparison (`/flow` session, 2026-09-12).
- Unrelated to the `rename-functions-to-web.md` epic beyond the sequencing order defined
  in the publishing epic (the rename precedes the first publish, this test epic also
  precedes it — the three epics share the same "before the first real publish" milestone).
