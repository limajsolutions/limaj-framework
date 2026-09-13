# Bootstrap Allowlist (Source of Truth)

- Source (read-only): `/media/tvlima/STORAGE/Repos/<legacy-source-repo>`
- Target: `./` (current repository `limaj-framework`)
- Rule: copy only explicitly allowlisted items; never copy the entire repository.

## Copy now (generic files)

- `.claude/commands/analyst.md`
- `.claude/commands/spike.md`
- `.claude/commands/issues.md`
- `.claude/commands/impl.md`
- `.claude/commands/bugfix.md`
- `.claude/commands/review.md`
- `.claude/commands/infra.md`
- `.claude/commands/flow.md`

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
