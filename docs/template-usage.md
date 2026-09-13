# Using template-backend

Guide on how to consume and evolve `template-backend/` — the scaffold copied to start
a new SaaS product (see the full step-by-step in the [root README](../README.md)).

## Product slash commands

`template-backend/.claude/commands/` ships 6 commands, each with an exclusive,
non-overlapping responsibility:

| Command | Role |
|---|---|
| `/analyst` | Functional business rules, edge cases, and legal/compliance risks of a proposal |
| `/arquiteto` | Technical design, stack, TDD, security/DevSecOps — Markdown-only output |
| `/flow` | Mediates the discussion between `/arquiteto` and `/analyst` until consensus; formalizes the epic |
| `/spike` | The only agent that changes product code — investigates, implements, fixes bugs, reviews |
| `/qa` | Independent test execution, oriented by epics/documentation/code |
| `/infra` | Automation, local dev environment, and maintenance of `.claude/` |

See the full description of each role in the root [`CLAUDE.md`](../CLAUDE.md) (section
"Prompt / command surfaces") and the content of each file under `template-backend/.claude/commands/`.

## Task management: epics in `docs/epics/`

The product generated from the template **does not use an external board** (GitHub Projects, Jira)
as its task-management system — it uses `docs/epics/`, versioned alongside the code.
`template-backend/docs/` already ships with the skeleton in place:

```
docs/
  README.md              # functional/technical index + epic lists by status
  epics/
    backlog/               # formalized by /flow, implementation not started
    em-andamento/          # implementation in progress (moved by /spike)
    finalizados/           # implemented phases (moved by /spike)
```

**Lifecycle of an epic:**

1. The developer brings an idea/question/bug to `/flow`.
2. `/flow` gathers independent opinions from `/arquiteto` and `/analyst`, cross-checks them,
   and seeks consensus (at most 1 round of counter-argument; real conflict escalates to the
   developer to decide).
3. If the point becomes concrete work, `/flow` writes `docs/epics/backlog/{slug}.md` —
   context, architectural decisions (`DA-###`), structure per layer, and a phase checklist
   with `- [ ]`.
4. The developer triggers `/spike` pointing at the epic. On start, `/spike` moves the file
   to `docs/epics/em-andamento/` and adds the entry to `docs/README.md`.
5. `/spike` implements phase by phase, marking `- [x]` for each completed item, always with
   tests and (if there's a schema change) the migration applied in the same turn.
6. When the last phase is done, `/spike` moves the epic to `docs/epics/finalizados/` and updates
   `docs/README.md`. This is implementation bookkeeping — **not** business approval;
   functional sign-off remains the user's.
7. `/qa` validates the completed epic using the file's own acceptance criteria as the test script.

This convention is based on real usage in products already built from this framework —
see `bootstrap/allowlist.md` for what was imported from the legacy source repository.

## What's still missing here

- `template-backend/src/` and `template-backend/test/` are still empty (`.gitkeep` only) — see
  the honesty note in the [root README](../README.md#starting-a-new-saas-from-this-scaffold-step-by-step).
- Testing conventions (xUnit/Moq/AutoFixture) are up to each product's `/arquiteto`,
  documented in the product's `CLAUDE.md` once the stack is defined.
