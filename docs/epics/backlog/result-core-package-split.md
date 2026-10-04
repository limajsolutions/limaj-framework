# Split the result core out of Limaj.Framework.Abstractions

**Status:** Backlog — not yet mediated; not business-approved (that is the user's decision)
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

## Open decisions

> Pending decision: DA-001 — whether to split at all, and if so: package name, minor with
> `TypeForwardedTo` versus a clean break in 3.0.0, and what moves (does
> `IExceptionToErrorMapper` go with the core?). To be settled in a dedicated `/flow` point.

## Phase checklist

### Phase 1 — Decide
- [ ] Mediate DA-001 in `/flow`, then fill in the structure, tests and implementation phases
