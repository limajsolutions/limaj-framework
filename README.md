# Limaj Framework

Reusable base for accelerating the creation of SaaS backends in .NET/Azure Functions, focused on:

- generic technical building blocks (`packages/Limaj.Framework.*`)
- a backend project template (`template-backend/`), with an isolated Dev Container for Claude Code
- Claude Code slash commands (`.claude/commands/`) loaded as specialized sessions

## Repository structure

- `packages/Limaj.Framework.Abstractions`: contracts, common types, and base errors.
- `packages/Limaj.Framework.Application`: application layer decoupled from concrete infrastructure.
- `packages/Limaj.Framework.Persistence.EFCore`: EF Core adapters implementing the abstraction contracts.
- `packages/Limaj.Framework.Web`: generic HTTP pipeline, host-agnostic (Azure Functions isolated worker or Minimal API — both speak `HttpRequest`/`IResult`).
- `template-backend/`: scaffold for starting new products — already ships `.claude/commands/`, `docs/epics/{backlog,em-andamento,finalizados}/`, and `docs/README.md`.
- `docs/template-usage.md`: how to consume/evolve the template, including the epic-management lifecycle under `docs/epics/`.
- `Limaj.Framework.sln`: framework solution for building/testing the base packages.

## Starting a new SaaS from this scaffold (step-by-step)

End-to-end executable guide. Follow it in order; each block is copy-pasteable. The example
uses the product name **`Acme`** — swap it for `YourSaas` freely.

> **Prerequisites:** `git`, `docker`, `dotnet` SDK 9, VS Code with the **Dev Containers**
> extension, and the `gh` CLI (optional, only to create the repo on GitHub).
>
> **Honesty note:** today `template-backend/src/` and `template-backend/test/`
> only contain empty folders (`.gitkeep`) — **there is no `.csproj`/`.sln` ready yet**. Steps 4–5
> create the real .NET structure. Once the template ships ready-made projects, steps 4–5
> will become just "rename".

### 1. Create the product repository from the template

Copy **only** the contents of `template-backend/` into a new repository (it already ships
`.claude/commands/`, `.devcontainer/`, `.vscode/`, `src/`, `test/`, `.github/`):

```bash
# set the product name (no spaces; used in folders and namespaces)
PRODUCT=Acme

# clone the framework and copy the scaffold into the new product's folder
git clone https://github.com/thalleslima8/limaj-framework.git
cp -r limaj-framework/template-backend "$PRODUCT"
cd "$PRODUCT"

# initialize the product's repo from scratch (without the framework's history)
rm -rf .git
git init -b main
```

### 2. Rename `Template` → the product name

Bulk-rename folders, files, and content. Handles **both cases**: `Template`
(namespaces, projects) → `Acme`, and lowercase `template` (Docker network/volumes/
container_name, database name) → `acme` — otherwise two products would collide on
the same Docker network/volume. Run it at the root of the product's repo:

```bash
PRODUCT=Acme                                          # same name as step 1
PRODUCT_LC=$(echo "$PRODUCT" | tr '[:upper:]' '[:lower:]')

# 2a. file contents (namespaces, Docker identifiers, paths in scripts/configs)
grep -rl --binary-files=without-match -i "template" . \
  | grep -v '/.git/' \
  | xargs -r sed -i -e "s/Template/$PRODUCT/g" -e "s/template/$PRODUCT_LC/g"

# 2b. folder and file names (bottom-up: children before parents)
find . -depth -iname '*template*' -not -path './.git/*' | while read -r p; do
  newbase=$(basename "$p" | sed -e "s/Template/$PRODUCT/g" -e "s/template/$PRODUCT_LC/g")
  mv "$p" "$(dirname "$p")/$newbase"
done
```

Check nothing is left behind (should return empty):

```bash
grep -rln -i "template" . | grep -v '/.git/'
```

### 3. Choose the Dev Container stack

Edit the environment stack — **without rewriting the container**. Details and tables in
the [devcontainer guide](template-backend/.devcontainer/README.md) (in the product; section
"Switching stacks"). Summary:

| Decision | Where to change it |
|---|---|
| SQL Server database (default) ↔ PostgreSQL | `dockerComposeFile` in `.devcontainer/devcontainer.json` (swap `compose.sqlserver.yml` for `compose.postgres.yml`) + `DB_ENGINE` and build args in `.devcontainer/docker-compose.yml` |
| Functions backend (default) ↔ Kestrel API | `BACKEND_KIND` in `.devcontainer/docker-compose.yml` |
| Azure Storage (Azurite) | include/remove `compose.azurite.yml` + `USE_AZURITE` |
| Frontend (Node + Vite) | `USE_FRONTEND` + `INSTALL_NODE` |

For an **API + PostgreSQL with no frontend**, for example: use `compose.postgres.yml`,
`DB_ENGINE=postgres`, `BACKEND_KIND=api`, `USE_AZURITE=false`, `USE_FRONTEND=false`,
and in the build args `INSTALL_MSSQL=false INSTALL_PSQL=true INSTALL_FUNC=false`.

### 4. Create the solution and .NET projects

The template doesn't yet ship `.csproj`/`.sln` — create them now (adjust to your choice from step 3).
Example for the default stack (Functions + SQL Server):

```bash
PRODUCT=Acme
dotnet new sln -n "$PRODUCT"

# reference the framework building blocks (local ProjectReference to the clone).
# Adjust the ../limaj-framework path if you moved the clone.
FW=../limaj-framework/packages

# create the real projects inside the folders already renamed under src/
# (replace the empty .gitkeep folders with the actual .NET structure)
# -> use whichever templates make sense: classlib for Domain/Application/Infrastructure,
#    func for Functions (or webapi if BACKEND_KIND=api).
```

> This step depends on your choices and on the `dotnet new` templates installed. Use the
> **`/arquiteto`** agent (step 7) to generate the concrete project structure and the
> correct `ProjectReference`s to `$FW/Limaj.Framework.*`, respecting the boundaries in the
> [Architectural boundaries](#architectural-boundaries) section.

### 5. Pin the `dotnet-ef` tool (needed for migrations in the Dev Container)

`post-create.sh` runs `dotnet tool restore` expecting `dotnet-ef` as a **local tool**.
Create the manifest once:

```bash
dotnet new tool-manifest          # creates .config/dotnet-tools.json
dotnet tool install dotnet-ef     # pins the version used by the team
```

### 6. Open in the Dev Container

```bash
code .
```

In VS Code: **Reopen in Container**. The first build downloads the image and runs
`post-create.sh` (restore, dummy secrets, local config, migrations). Once it's done, run
`claude` in the integrated terminal — **with no permission flag** (the broad allowlist
lives only inside the container; see the [devcontainer guide](template-backend/.devcontainer/README.md)).

### 7. Use the Claude slash commands

The commands already ship in the product's `.claude/commands/` — 6 commands, each with an
exclusive responsibility (see the breakdown in [`CLAUDE.md`](CLAUDE.md#prompt--command-surfaces)):

| Command | Purpose |
|---|---|
| `/analyst` | functional business rules, edge cases, and legal/compliance risks → epic draft |
| `/arquiteto` | architecture, stack, TDD, and security/DevSecOps — Markdown output only |
| `/flow` | mediates the discussion between `/arquiteto` and `/analyst` until consensus; formalizes the epic in `docs/epics/` |
| `/spike` | the only agent that changes product code — investigates, implements, fixes bugs, and reviews |
| `/qa` | tests the app (Playwright MCP, if `web/` exists) independently, oriented by epics/docs/code |
| `/infra` | maintains scripts, devcontainer, tooling, and `.claude/` itself |

Task management happens in `docs/epics/` (not an external board) — see
[`docs/template-usage.md`](docs/template-usage.md) for the full epic lifecycle.

### 8. Validate and make the first commit

```bash
dotnet build                       # build the product's solution
dotnet test                        # once test projects exist
git add -A && git commit -m "chore: initial scaffold from limaj-framework"

# (optional) publish to GitHub
gh repo create "$PRODUCT" --private --source=. --push
```

## Architectural boundaries

Allowed dependency direction (must not be broken):

1. `Abstractions` → no internal dependencies.
2. `Application` → `Abstractions` only.
3. `Persistence.EFCore` → `Abstractions`.
4. `Web` (or `Api`) → `Abstractions` + web/functions stack.

`Application` never references concrete persistence, host identity, or
infrastructure. If a concrete implementation is needed, define the contract in
`Abstractions` and put the adapter in the infrastructure layer.

Building blocks to reuse (not reinvent): `Result`/`Error` for the return flow,
persistence contracts for the services, and the generic HTTP pipeline (`RequestRunner`)
for endpoints. See [CLAUDE.md](CLAUDE.md) for the full pattern catalog.

## Keeping the commands in sync (only when working on THIS repo)

The canonical copy of the commands is `template-backend/.claude/commands/` (that's what
travels to each SaaS). The framework root has a mirror so the commands stay active when
opening limaj itself. After editing the canonical copy:

```bash
bash template-backend/scripts/sync-commands.sh        # mirror template → root
bash template-backend/scripts/sync-commands.sh --check # only check for drift (CI)
```

## Validating the framework itself

```bash
dotnet build Limaj.Framework.sln
```

## Publishing the packages (`Limaj.Framework.*`)

The 4 packages (`Abstractions`, `Application`, `Persistence.EFCore`, `Web`) are published in
lockstep (same version, one tag per release) to this repository's **GitHub Packages**
feed, via [`.github/workflows/publish-packages.yml`](.github/workflows/publish-packages.yml).
Full decisions in
[`docs/epics/finalizados/nuget-package-publishing-pipeline.md`](docs/epics/finalizados/nuget-package-publishing-pipeline.md).

### Pre-release (automatic)

Every push to `main` that passes `restore → build → test` generates and publishes a
pre-release package (`X.Y.Z-alpha.0.<height>+sha.<short-commit>`) — no promise of
stability, just continuous feedback traceable back to the commit. If `test` fails, nothing
is published.

### Stable release (manual cut, no formal gate)

There's no automation or mandatory checklist for deciding "when" to cut a tag —
it's up to whoever is cutting it (DA-006). The mechanism:

```bash
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0
```

Pushing the tag (`vX.Y.Z` pattern) triggers the same pipeline; since MinVer resolves the
version exactly at the tag, the package comes out without a pre-release suffix (`1.2.0`,
not `1.2.0-alpha...`).

### Consuming the packages from another repository

#### 1. Authenticate against the feed

GitHub Packages requires authentication even for reads. In the consumer repository
(the product that will reference `Limaj.Framework.*`, **not** this repo), create a
fine-grained PAT scoped to `read:packages` only and configure:

```xml
<!-- nuget.config of the consumer product -->
<configuration>
  <packageSources>
    <add key="limaj-framework" value="https://nuget.pkg.github.com/limajsolutions/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <limaj-framework>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="%LIMAJ_FRAMEWORK_PAT%" />
    </limaj-framework>
  </packageSourceCredentials>
</configuration>
```

Never commit the PAT in plain text — use an environment variable (`LIMAJ_FRAMEWORK_PAT`
above, resolved by NuGet during `dotnet restore`) or a CI secret in the consumer product.

#### 2. Choose a version

The consumption model is **pinned version, deliberate upgrade** (DA-003) — no floating
range or auto-update. Two version options to reference:

- **Stable** (`X.Y.Z`, e.g. `1.2.0`) — only exists once someone cuts the corresponding
  tag (see "Stable release" above). This is what a production product should use.
- **Pre-release** (`X.Y.Z-alpha.0.<height>+sha.<commit>`, e.g.
  `1.2.0-alpha.0.4+sha.a1b2c3d`) — published on every push to `main`; only useful for
  testing a recent change before a stable tag exists, never for production. See the
  available versions (stable and pre-release) under the **Packages** tab of this
  repository at `https://github.com/limajsolutions/limaj-framework/packages`.

#### 3. Reference the packages in the project

Each package corresponds to a layer — reference only the one(s) your project uses,
respecting the same dependency direction described in
[Architectural boundaries](#architectural-boundaries) (e.g., a product's `Application`
project references `Limaj.Framework.Application`, not `Limaj.Framework.Web`):

```bash
# product's application/domain project
dotnet add package Limaj.Framework.Abstractions --version 1.2.0 --source limaj-framework
dotnet add package Limaj.Framework.Application --version 1.2.0 --source limaj-framework

# product's persistence (EF Core) project
dotnet add package Limaj.Framework.Persistence.EFCore --version 1.2.0 --source limaj-framework

# product's HTTP host project (Azure Functions isolated worker or Minimal API)
dotnet add package Limaj.Framework.Web --version 1.2.0 --source limaj-framework
```

Direct equivalent in `.csproj`, if you prefer editing manually instead of `dotnet add`:

```xml
<ItemGroup>
  <PackageReference Include="Limaj.Framework.Abstractions" Version="1.2.0" />
  <PackageReference Include="Limaj.Framework.Application" Version="1.2.0" />
</ItemGroup>
```

Since the 4 packages ship in lockstep (DA-002), always reference **the same version
number** across every project of the product that consumes `Limaj.Framework.*` — there is
no supported scenario today for mixing different versions between them.

### Incident: bad version published

1. Immediately publish the fixed version (new tag `vX.Y.Z+1`) — never overwrite
   the bad version, SemVer doesn't allow reusing the same number.
2. Remove the bad version from the feed: the package's page at
   `https://github.com/orgs/limajsolutions/packages` (or the user's, if the package is
   under a personal account) → the specific version → **Delete version** (requires
   admin permission on the package). Unlike `nuget.org`, GitHub Packages allows real
   deletion, not just "unlisting".
3. Notify package consumers (the teams owning the products) to update their
   reference to the fixed version.

### Public feed (`nuget.org`)

Out of scope for now — only consider it if and when there's real demand from a
consumer outside the team (see DA-001).

## Guardrails

- Do not introduce product-specific domain code inside the framework.
- Do not add product seed data to the base framework.
- If a concrete implementation is needed, create a contract in the abstractions and an adapter in the infrastructure layer.
