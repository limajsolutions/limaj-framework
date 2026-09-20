# Bootstrap Allowlist (Source of Truth)

- Source (read-only): `/media/tvlima/STORAGE/Repos/<legacy-source-repo>`
- Target: `./` (current repository `limaj-framework`)
- Rule: copy only explicitly allowlisted items; never copy the entire repository.

## Copy now (generic files)

- *(none pending)*

> **Claude Code commands/agents/skills are no longer imported into this repository.** The
> original bootstrap copied the legacy `.claude/commands/*.md` files here; they have since
> been generalized and moved to
> [thalleslima8/my-skills](https://github.com/thalleslima8/my-skills) (`workflow` plugin),
> and the generic `.claude/settings.json` + `CLAUDE.md` starting point lives in
> [thalleslima8/ai-starter-kit](https://github.com/thalleslima8/ai-starter-kit). Do not
> re-import them here — port improvements to `my-skills` instead.

## Adapt before copying

- Any file that contains:
- product names, examples, paths, or specific context
- specific namespaces/projects (e.g., solution names, assemblies, csproj)
- product/domain references (entities, business rules, legacy backlog)
- coupled environment configuration (resource names, real IDs, URLs)

## Do not copy

- product seed-data artifacts
- any file/folder with a name or content specific to a product
- domain code, business features, and legacy migration
- secrets, sensitive variables, and real environment files
