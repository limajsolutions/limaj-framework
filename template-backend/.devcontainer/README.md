# Dev Container isolado para o Claude Code

Ambiente de desenvolvimento containerizado onde o Claude Code opera **sem prompts de
permissão** e **sem risco para a máquina host**. É parte do `template-backend`: ao criar
um SaaS novo a partir deste gabarito, você herda este ambiente e só ajusta o **stack**
(banco, storage, tipo de backend, frontend) — sem "codar um container do zero".

> Este documento é o guia operacional. As decisões de design (por que isolado, por que
> sem socket do Docker, por que secrets dummy) estão na seção [Modelo de isolamento](#modelo-de-isolamento).

---

## 1. Como usar (depois de clonado o produto)

1. Tenha **Docker** + **VS Code** com a extensão **Dev Containers**.
2. Abra o repo no VS Code → `Reopen in Container`.
3. Aguarde o build da imagem + o `post-create.sh` (restore, secrets dummy, migrations).
4. Rode `claude` no terminal integrado — **sem nenhuma flag de permissão**.

O primeiro build é lento (baixa SDK/Node/tools); rebuilds são rápidos (cache de camadas).

---

## 2. O que sobe

| Serviço | Origem | Acesso interno | Exposto ao host? |
|---|---|---|---|
| `app` | `Dockerfile` (onde o VS Code/Claude rodam) | — | via forwardPorts (túnel VS Code) |
| `sqlserver` *ou* `postgres` | fragmento de compose | `sqlserver:1433` / `postgres:5432` | **não** |
| `azurite` (opcional) | fragmento de compose | `azurite:10000-10002` | **não** |

**Comunicação por hostname de serviço, nunca `localhost`.** Nenhum serviço publica porta
no Docker (`ports:`), então este ambiente **convive** com um `devops/` local rodando em
paralelo sem colisão de porta. O acesso do host ao que roda *dentro* do `app` (frontend,
API) é pelo `forwardPorts` do `devcontainer.json` — um túnel do VS Code, não do Docker.

---

## 3. Trocar de stack

Toda a parametrização fica em **dois lugares**: a lista de fragmentos no `devcontainer.json`
e as envs/args no `docker-compose.yml`. Nenhuma exige editar lógica de script.

### 3.1 Banco: SQL Server ↔ PostgreSQL

| Passo | SQL Server (default) | PostgreSQL |
|---|---|---|
| `devcontainer.json` → `dockerComposeFile` | `compose.sqlserver.yml` | troque por `compose.postgres.yml` |
| `docker-compose.yml` → `app.environment.DB_ENGINE` | `sqlserver` | `postgres` |
| `docker-compose.yml` → build args | `INSTALL_MSSQL: "true"` | `INSTALL_MSSQL: "false"`, `INSTALL_PSQL: "true"` |

O `post-create.sh` deriva a connection string certa a partir de `DB_ENGINE`
(`Server=sqlserver,1433;...` vs `Host=postgres;Port=5432;...`). **Sem banco?**
`DB_ENGINE=none` (pula migrations e a connection string) e remova o fragmento de DB.

### 3.2 Backend: Azure Functions ↔ API ASP.NET Core

| | Functions (default) | API (Kestrel) |
|---|---|---|
| `BACKEND_KIND` | `functions` | `api` |
| Config gerada | `src/Template.Functions/local.settings.json` | `src/Template.Api/appsettings.Development.json` |
| Build arg | `INSTALL_FUNC: "true"` | `INSTALL_FUNC: "false"` |
| Debug | terminal `func host start` + **Attach** | config **Launch API** do `launch.json` |
| Porta | 7071 | 5000/5001 |

### 3.3 Storage Azure (Azurite)

Inclua `compose.azurite.yml` e mantenha `USE_AZURITE=true` **só** se o backend usa Blob/
Queue/Table (ex.: Functions com Timer/Queue trigger). Senão, remova o fragmento e
`USE_AZURITE=false` — o `AzureWebJobsStorage` deixa de ser escrito na config.

### 3.4 Frontend (Node + Vite)

`USE_FRONTEND=true` roda `npm ci` em `web/` no bootstrap; `INSTALL_NODE: "true"` põe Node
24 na imagem. API pura sem frontend: ambos `false` (a imagem fica menor e o Claude ainda
é instalado via instalador nativo).

---

## 4. O que o `post-create.sh` faz (idempotente)

`dotnet restore` → (`npm ci` se frontend) → gera secrets **dummy** e escreve a config local
(`local.settings.json` ou `appsettings.Development.json`) → instala a allowlist do Claude no
container → `dotnet tool restore` → `dotnet ef database update` (com retry até o banco ficar
pronto). Rodar de novo não duplica nem corrompe nada.

Armadilhas já resolvidas (não reintroduza ao adaptar):
- **Connection string interna** (`sqlserver`/`postgres`, não `localhost`) — sem isso o app não acha o banco.
- **`ConnectionStrings__Default` exportado** antes do `dotnet ef` — em design-time o EF ignora o config do host.
- **Regeneração de `local.settings.json` cifrado** — config cifrada no host não descriptografa no container.
- **Azurite por connection string explícita** — `UseDevelopmentStorage=true` aponta para 127.0.0.1 e não acha o serviço.

---

## 5. Modelo de isolamento

O Claude **pode** livremente: editar o workspace, `dotnet build/test/ef`, subir `func`/
`vite`/API, conectar no banco e no Azurite internos, e até estragar o próprio container.

O Claude **não alcança**: o filesystem do host fora do workspace, o Docker Engine do host
(o socket `/var/run/docker.sock` **não** é montado), outros containers do host, segredos
reais (só dummies entram), bancos/storage de produção.

Por isso a allowlist ampla (`claude-settings.json`, com `bypassPermissions`) é segura:
ela vive **só** dentro do container (o `post-create.sh` a copia para `~/.claude/settings.json`).
**Nunca** a mergeie no `.claude/settings.json` versionado do host.

**Recuperação de desastre:** se algo corromper, use `Rebuild Container`. Dados de banco
vivem em volume `*_dev` separado — apague o volume e o `post-create` recria o schema.

---

## 6. Adaptar ao renomear o template

Ao trocar `Template` → nome do produto, ajuste também:
- `docker-compose.yml` e fragmentos: rede `template_devnet`, `container_name *-dev`, volumes `*_dev`.
- `post-create.sh`: `PROJECT_NAME` e os caminhos dos projetos.
- `launch.json`: caminho do `.dll` da API.
- `devcontainer.json`: o `name`.

---

## 7. Arquivos

```
.devcontainer/
├── devcontainer.json        # serviço-alvo, fragmentos, forwardPorts, postCreate
├── docker-compose.yml       # BASE: só o serviço `app` (+ envs de parametrização)
├── compose.sqlserver.yml    # fragmento DB — SQL Server 2022
├── compose.postgres.yml     # fragmento DB — PostgreSQL 16
├── compose.azurite.yml      # fragmento storage — Azurite (opcional)
├── Dockerfile               # imagem do `app`; camadas opcionais via build args
├── post-create.sh           # bootstrap idempotente, parametrizado por env
├── claude-settings.json     # allowlist ampla — SÓ dentro do container
└── README.md                # este guia
.vscode/
└── launch.json              # attach (Functions) / launch (API Kestrel)
```
