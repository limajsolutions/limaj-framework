# Isolated Dev Container for Claude Code

Containerized development environment where Claude Code operates **without permission
prompts** and **without risk to the host machine**. It is part of `template-backend`:
when creating a new SaaS from this scaffold, you inherit this environment and only need
to adjust the **stack** (database, storage, backend kind, frontend) — no "building a
container from scratch".

> This document is the operational guide. Design decisions (why isolated, why no
> Docker socket, why dummy secrets) are in the [Isolation model](#5-isolation-model) section.

---

## 1. How to use (after cloning the product)

1. Have **Docker** + **VS Code** with the **Dev Containers** extension.
2. Open the repo in VS Code → `Reopen in Container`.
3. Wait for the image build + `post-create.sh` (restore, dummy secrets, migrations).
4. Run `claude` in the integrated terminal — **with no permission flag**.

The first build is slow (downloads SDK/Node/tools); rebuilds are fast (layer cache).

---

## 2. What comes up

| Service | Source | Internal access | Exposed to host? |
|---|---|---|---|
| `app` | `Dockerfile` (where VS Code/Claude run) | — | via forwardPorts (VS Code tunnel) |
| `sqlserver` *or* `postgres` | compose fragment | `sqlserver:1433` / `postgres:5432` | **no** |
| `azurite` (optional) | compose fragment | `azurite:10000-10002` | **no** |

**Communication via service hostname, never `localhost`.** No service publishes a port
on Docker (`ports:`), so this environment **coexists** with a local `devops/` running in
parallel without port collisions. Host access to what runs *inside* `app` (frontend,
API) goes through `forwardPorts` in `devcontainer.json` — a VS Code tunnel, not Docker's.

---

## 3. Switching stacks

All parametrization lives in **two places**: the list of fragments in `devcontainer.json`
and the envs/args in `docker-compose.yml`. None of them require editing script logic.

### 3.1 Database: SQL Server ↔ PostgreSQL

| Step | SQL Server (default) | PostgreSQL |
|---|---|---|
| `devcontainer.json` → `dockerComposeFile` | `compose.sqlserver.yml` | swap for `compose.postgres.yml` |
| `docker-compose.yml` → `app.environment.DB_ENGINE` | `sqlserver` | `postgres` |
| `docker-compose.yml` → build args | `INSTALL_MSSQL: "true"` | `INSTALL_MSSQL: "false"`, `INSTALL_PSQL: "true"` |

`post-create.sh` derives the correct connection string from `DB_ENGINE`
(`Server=sqlserver,1433;...` vs `Host=postgres;Port=5432;...`). **No database?**
`DB_ENGINE=none` (skips migrations and the connection string) and remove the DB fragment.

### 3.2 Backend: Azure Functions ↔ ASP.NET Core API

| | Functions (default) | API (Kestrel) |
|---|---|---|
| `BACKEND_KIND` | `functions` | `api` |
| Generated config | `src/Template.Functions/local.settings.json` | `src/Template.Api/appsettings.Development.json` |
| Build arg | `INSTALL_FUNC: "true"` | `INSTALL_FUNC: "false"` |
| Debug | `func host start` terminal + **Attach** | `launch.json`'s **Launch API** config |
| Port | 7071 | 5000/5001 |

### 3.3 Azure Storage (Azurite)

Include `compose.azurite.yml` and keep `USE_AZURITE=true` **only** if the backend uses Blob/
Queue/Table (e.g., Functions with a Timer/Queue trigger). Otherwise, remove the fragment and
set `USE_AZURITE=false` — `AzureWebJobsStorage` is no longer written to the config.

### 3.4 Frontend (Node + Vite)

`USE_FRONTEND=true` runs `npm ci` in `web/` during bootstrap; `INSTALL_NODE: "true"` puts
Node 24 in the image. For a pure API with no frontend: both `false` (the image stays
smaller and Claude is still installed via the native installer).

---

## 4. What `post-create.sh` does (idempotent)

`dotnet restore` → (`npm ci` if frontend) → generates **dummy** secrets and writes the local
config (`local.settings.json` or `appsettings.Development.json`) → installs the Claude
allowlist in the container → `dotnet tool restore` → `dotnet ef database update` (with retry
until the database is ready). Running it again doesn't duplicate or corrupt anything.

Pitfalls already solved (don't reintroduce them when adapting):
- **Internal connection string** (`sqlserver`/`postgres`, not `localhost`) — without this the app can't find the database.
- **`ConnectionStrings__Default` exported** before `dotnet ef` — at design time, EF ignores the host's config.
- **Regenerating an encrypted `local.settings.json`** — config encrypted on the host won't decrypt inside the container.
- **Azurite via explicit connection string** — `UseDevelopmentStorage=true` points to 127.0.0.1 and can't find the service.

---

## 5. Isolation model

Claude **can** freely: edit the workspace, `dotnet build/test/ef`, bring up `func`/
`vite`/API, connect to the internal database and Azurite, and even break the container itself.

Claude **cannot reach**: the host filesystem outside the workspace, the host's Docker Engine
(the `/var/run/docker.sock` socket is **not** mounted), other host containers, real secrets
(only dummies go in), or production databases/storage.

That's why the broad allowlist (`claude-settings.json`, with `bypassPermissions`) is safe:
it lives **only** inside the container (`post-create.sh` copies it to `~/.claude/settings.json`).
**Never** merge it into the host's versioned `.claude/settings.json`.

**Disaster recovery:** if something breaks, use `Rebuild Container`. Database data lives
in a separate `*_dev` volume — delete the volume and `post-create` recreates the schema.

---

## 6. Adapting when renaming the template

When swapping `Template` → the product name, also adjust:
- `docker-compose.yml` and fragments: `template_devnet` network, `container_name *-dev`, `*_dev` volumes.
- `post-create.sh`: `PROJECT_NAME` and the project paths.
- `launch.json`: the API's `.dll` path.
- `devcontainer.json`: the `name`.

---

## 7. Files

```
.devcontainer/
├── devcontainer.json        # target service, fragments, forwardPorts, postCreate
├── docker-compose.yml       # BASE: just the `app` service (+ parametrization envs)
├── compose.sqlserver.yml    # DB fragment — SQL Server 2022
├── compose.postgres.yml     # DB fragment — PostgreSQL 16
├── compose.azurite.yml      # storage fragment — Azurite (optional)
├── Dockerfile               # `app` image; optional layers via build args
├── post-create.sh           # idempotent bootstrap, parametrized by env
├── claude-settings.json     # broad allowlist — ONLY inside the container
└── README.md                # this guide
.vscode/
└── launch.json              # attach (Functions) / launch (API Kestrel)
```
