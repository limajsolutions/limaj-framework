# Changelog

All notable changes to this framework are recorded here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/); since this is an internal
framework with no publicly versioned releases, entries are organized by date.

## [Unreleased]

### Changed
- **Consolidated slash commands from 10 to 6**, each with an exclusive, non-overlapping responsibility: `/analyst`, `/arquiteto`, `/flow`, `/spike`, `/qa`, `/infra`.
  - `/spike` absorbed `/dev` (implementation), `/bugfix` (fixing), and `/review` (code review) — it is now the only agent authorized to touch product code, operating in modes (investigation, epic implementation, direct implementation, bug fix, review).
  - `/flow` stopped being the orchestrator of the full cycle (`analyst → spike → SM → dev → bugfix`) and became exclusively the mediator between `/arquiteto` and `/analyst`: independent opinions, cross-check, at most 1 round of counter-argument, escalation to the user if the conflict persists.
  - `/infra` explicitly became the owner of everything under `.claude/` (commands, settings, canonical→root sync), plus the dev environment and automation.
  - `/qa` explicitly became independent, oriented by epics/documentation/code, with full autonomy over the local database's test data (never production).
  - `/arquiteto` gained a dedicated security/DevSecOps section (AuthN/AuthZ, sensitive data, attack surface, CI/CD and supply chain, retention/privacy).
  - `/analyst` gained a compliance/legal posture (privacy, data retention, legal exposure) as part of its critical checklist.
- `/SM`, `/dev`, `/bugfix`, and `/review` were **removed** from `.claude/commands/` (canonical in `template-backend/`, mirrored at the root via `sync-commands.sh`).

### Added
- Work-management convention via `docs/epics/{backlog,em-andamento,finalizados}/` replacing GitHub Issues/Projects as the tracking system — documented in [`docs/template-usage.md`](docs/template-usage.md) and referenced in `CLAUDE.md`.
- `template-backend/docs/README.md` and the `docs/epics/` folder skeleton — now ship ready-made with every new product created from the template.

## [2026-06-06] — Isolated Dev Container + initial slash commands

### Added
- Parametrizable Dev Container for `template-backend/` (Docker socket not mounted, broad allowlist isolated in `claude-settings.json`), with swappable stacks via `docker-compose` fragments (SQL Server, PostgreSQL, Azurite) and an idempotent `post-create.sh`.
- First batch of slash commands under `.claude/commands/`: `/analyst`, `/arquiteto`, `/spike`, `/SM`, `/dev`, `/bugfix`, `/review`, `/qa`, `/infra`, `/flow`.
- `template-backend/scripts/sync-commands.sh` to keep the canonical copy (`template-backend/.claude/commands/`) and the root mirror (`.claude/commands/`) in sync.
- Frontend CI and quality checks as placeholders (`echo "TODO: ..."`) under `template-backend/.github/workflows/`.
- Initial READMEs describing the purpose of each template component.
