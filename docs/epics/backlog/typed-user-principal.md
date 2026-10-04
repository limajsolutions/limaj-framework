# Typed, extensible user principal for IUserIdentityGateway

**Status:** Backlog — low priority; not yet mediated; not business-approved (that is the user's decision)
**Última revisão:** 2026-10-04

## Context

This comes from the same consumer request that produced `web-error-extensibility.md`, where it
was listed as item **A4**, desirable and low priority.

`IUserIdentityGateway` (`packages/Limaj.Framework.Abstractions/src/Identity/`) returns only a
`string?` user id today. The consumer asked for a typed principal that products can extend.

In the `/flow` session of 2026-10-04 this item was taken out of the error-extensibility point
by consensus, because it is unrelated to error handling. It has **not** gone through a mediation
round of its own yet.

## Initial positions (from that session, not decisions)

- **Architect.** Accept it as a separate, low-priority piece of work.
  - **Additive shape (minor):** a new interface (e.g. `ICurrentPrincipal<TPrincipal>`) or a
    default interface method.
  - **Breaking shape (major):** adding an abstract member to `IUserIdentityGateway` breaks
    every implementer.
  - **Constraint:** it needs its own design so that claims or host types do not leak into
    `Application`, as the package layering in `CLAUDE.md` requires.
- **Analyst.** Out of this point; low priority.
  - **Privacy:** a typed principal tends to carry personal data (e-mail, name, claims) into the
    application layer and into logs, so it needs its own privacy analysis.
  - **Versioning:** a new contract next to the current one is minor; changing
    `IUserIdentityGateway`'s signature is major.

## Open decisions

> Pending decision: DA-001 — shape of the typed principal (new generic contract versus an
> extension of `IUserIdentityGateway`), which data it may carry, how it is kept out of logs,
> and its version. To be settled in a dedicated `/flow` point.

## Phase checklist

### Phase 1 — Decide
- [ ] Mediate DA-001 in `/flow`, then fill in the structure, tests and implementation phases
