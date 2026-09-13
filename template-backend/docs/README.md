# {Product} — Documentation

This index points to the functional and technical documentation aligned with the
current code. When copying `template-backend/` into a new product, fill in the
sections below — the folder skeleton (`docs/epics/{backlog,em-andamento,finalizados}/`)
is already in place.

## Index

- [Introduction](./introduction.md) — *(to create)* product overview and the problem it solves
- [Glossary](./glossary.md) — *(to create)* domain terms
- [Business Rules](./business-rules.md) — *(to create)* maintained by `/analyst`
- Technical/architectural documentation — maintained by `/arquiteto`, one `.md` per relevant decision or area

## Epic management — `docs/epics/`

`docs/epics/` is the product's task-management system — it replaces an external
board (GitHub Projects, Jira, etc.). Each epic is a `.md` with checkboxes, created
from consensus between `/analyst` and `/arquiteto` mediated by `/flow`, and
implemented by `/spike`.

**Folder flow:** `backlog/` (formalized, implementation not started) → `em-andamento/`
(implementation in progress) → `finalizados/` (implemented phases).

**Mandatory conventions** (mirrored from the product's `CLAUDE.md` — keep both in sync):
- `- [ ]` = pending; `- [x]` = done
- Mark `[x]` when a step is completed **before** the commit
- Update `Última revisão: YYYY-MM-DD` whenever the file changes
- New epic: `/flow` creates `docs/epics/backlog/{slug}.md` (no entry in this index yet)
- When implementation starts, `/spike` moves it from `backlog/` to `em-andamento/` and adds
  the corresponding line below, under "Epics in progress"
- When the last phase is completed, `/spike` moves it from `em-andamento/` to `finalizados/`
  and updates the corresponding line to "Epics completed"
- **Moving to `finalizados/` is implementation bookkeeping, not business approval** —
  functional sign-off remains the user's exclusive decision

## Backlog

Epics formalized via `/flow` but whose implementation has not started yet — see `docs/epics/backlog/`.
No entry here until they are moved to "Epics in progress".

*(no epics in the backlog at the moment)*

## Epics in progress

*(no epics in progress at the moment)*

## Epics completed

*(no epics completed at the moment)*

## Roadmap

The roadmap is maintained in the epic documents under `docs/epics/`. Each file contains
motivation, architectural decisions (`DA-###`), phases with checkboxes, and acceptance
criteria. This documentation describes implemented functional state and current contracts.
