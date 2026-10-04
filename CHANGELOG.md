# Changelog

All notable changes to this framework are recorded here. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/); since this is an internal
framework with no publicly versioned releases, entries are organized by date.

## [Unreleased]

### Removed
- **Claude Code commands, agents, and skills moved out of this repository.** Removed `.claude/commands/` (root mirror), `template-backend/.claude/commands/` (canonical copy), and `template-backend/scripts/sync-commands.sh` (no longer has anything to sync). They now live in [thalleslima8/my-skills](https://github.com/thalleslima8/my-skills) as the `workflow` plugin; the generic `.claude/settings.json` + `CLAUDE.md` starting point lives in [thalleslima8/ai-starter-kit](https://github.com/thalleslima8/ai-starter-kit).

### Changed
- **BREAKING — the 4 `Limaj.Framework.*` packages now target `net10.0` only** (previously `net9.0`), with EF Core / `Microsoft.Extensions.Configuration*` dependencies on `10.0.12`; the test projects and the publish workflow (`dotnet-version: 10.0.x`) moved too. Consumers must be on .NET 10 — release this as a new major version. No source changes were needed.
- Root `.claude/settings.json` (same content as ai-starter-kit) now declares the `my-skills` marketplace and enables `workflow@my-skills`, so the commands remain usable when working on the framework itself.
- `template-backend/.devcontainer/claude-settings.json` and `post-create.sh` now state explicitly that they cover permissions only; marketplace/plugins come from the project's `.claude/settings.json` (ai-starter-kit + my-skills).
- README "Starting a new SaaS" guide: new step 6 (bring in `.claude/` + `CLAUDE.md` from ai-starter-kit) and step 8 (install `workflow@my-skills`, plus `dotnet@my-skills` for EF Core). Removed the "Keeping the commands in sync" section.
- **Consolidated slash commands from 10 to 6**, each with an exclusive, non-overlapping responsibility: `/analyst`, `/arquiteto`, `/flow`, `/spike`, `/qa`, `/infra`.
  - `/spike` absorbed `/dev` (implementation), `/bugfix` (fixing), and `/review` (code review) — it is now the only agent authorized to touch product code, operating in modes (investigation, epic implementation, direct implementation, bug fix, review).
  - `/flow` stopped being the orchestrator of the full cycle (`analyst → spike → SM → dev → bugfix`) and became exclusively the mediator between `/arquiteto` and `/analyst`: independent opinions, cross-check, at most 1 round of counter-argument, escalation to the user if the conflict persists.
  - `/infra` explicitly became the owner of everything under `.claude/` (commands, settings, canonical→root sync), plus the dev environment and automation.
  - `/qa` explicitly became independent, oriented by epics/documentation/code, with full autonomy over the local database's test data (never production).
  - `/arquiteto` gained a dedicated security/DevSecOps section (AuthN/AuthZ, sensitive data, attack surface, CI/CD and supply chain, retention/privacy).
  - `/analyst` gained a compliance/legal posture (privacy, data retention, legal exposure) as part of its critical checklist.
- `/SM`, `/dev`, `/bugfix`, and `/review` were **removed** from `.claude/commands/` (canonical in `template-backend/`, mirrored at the root via `sync-commands.sh`). *(Historical: these files have since moved to my-skills — see "Removed" above.)*

### Added
- **Stable releases are now published to nuget.org** (public, no authentication to consume), via a new `publish-nuget-org` job in `publish-packages.yml` that runs only on `vX.Y.Z` tags and authenticates with nuget.org Trusted Publishing (GitHub OIDC → short-lived API key, environment `nuget`). Pre-releases from `main` stay on GitHub Packages only. Reverses DA-001's exclusion of nuget.org.
- Package metadata for the 4 `Limaj.Framework.*` packages (`packages/Directory.Build.props`): author `Limaj Solutions`, MIT license, repository/project URLs, tags, a package README (`packages/PACKAGE_README.md`), symbol packages (`.snupkg`) and deterministic CI builds; a per-package `Description` in each `.csproj`.
- `LICENSE` (MIT).
- Work-management convention via `docs/epics/{backlog,in-progress,done}/` replacing GitHub Issues/Projects as the tracking system — documented in [`docs/template-usage.md`](docs/template-usage.md) and referenced in `CLAUDE.md`.
- `template-backend/docs/README.md` and the `docs/epics/` folder skeleton — now ship ready-made with every new product created from the template.

### Fixed
- README consumption guide asked for a fine-grained PAT, which GitHub Packages' NuGet registry does not accept — it now asks for a classic PAT with `read:packages`, configured once per machine.

## [2026-06-06] — Isolated Dev Container + initial slash commands

### Added
- Parametrizable Dev Container for `template-backend/` (Docker socket not mounted, broad allowlist isolated in `claude-settings.json`), with swappable stacks via `docker-compose` fragments (SQL Server, PostgreSQL, Azurite) and an idempotent `post-create.sh`.
- First batch of slash commands under `.claude/commands/`: `/analyst`, `/arquiteto`, `/spike`, `/SM`, `/dev`, `/bugfix`, `/review`, `/qa`, `/infra`, `/flow`. *(Historical: since moved to my-skills — see "Removed" above.)*
- `template-backend/scripts/sync-commands.sh` to keep the canonical copy (`template-backend/.claude/commands/`) and the root mirror (`.claude/commands/`) in sync. *(Historical: removed together with the copies it synced — see "Removed" above.)*
- Frontend CI and quality checks as placeholders (`echo "TODO: ..."`) under `template-backend/.github/workflows/`.
- Initial READMEs describing the purpose of each template component.
