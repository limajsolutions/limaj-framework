#!/usr/bin/env bash
# Bootstrap idempotente do Dev Container isolado — ver .devcontainer/README.md.
#
# Roda uma vez após a criação do container (postCreateCommand). Pode rodar de novo
# sem corromper nada: secrets só são gerados se ainda não existirem; restore / npm ci
# / ef database update são naturalmente idempotentes.
#
# Comportamento PARAMETRIZADO pelas envs do serviço `app` (docker-compose.yml):
#   DB_ENGINE     sqlserver | postgres | none   → forma da connection string + espera do banco
#   BACKEND_KIND  functions | api               → local.settings.json (func) vs appsettings.Development.json
#   USE_AZURITE   true | false                  → AzureWebJobsStorage apontando para o serviço azurite
#   USE_FRONTEND  true | false                  → npm ci em web/
#
# NÃO entra nenhum segredo real aqui: chaves (Jwt etc.) são dummies aleatórios.
# Ao adaptar ao produto: ajuste PROJECT_NAME, os caminhos dos projetos e as chaves
# do bloco "Values" conforme a config que o seu backend realmente lê.

set -euo pipefail

# O postCreateCommand roda via `/bin/sh -c` (NÃO login shell), então /etc/profile.d/*
# não é lido e o PATH não inclui as ferramentas de usuário. Garantimos PATH explícito.
export PATH="$PATH:$HOME/.dotnet/tools:/opt/mssql-tools18/bin"

# ─── Parâmetros (env do serviço app, com defaults) ───
DB_ENGINE="${DB_ENGINE:-sqlserver}"
BACKEND_KIND="${BACKEND_KIND:-functions}"
USE_AZURITE="${USE_AZURITE:-true}"
USE_FRONTEND="${USE_FRONTEND:-true}"

# ─── Convenções de caminho (AJUSTE ao renomear o template) ───
WORKSPACE="${WORKSPACE:-/workspace}"
PROJECT_NAME="Template"                       # ex.: vira "MeuSaas" no produto
INFRA_PROJECT="$WORKSPACE/src/${PROJECT_NAME}.Infrastructure"
WEB_DIR="$WORKSPACE/web"

# Projeto de host (startup) e arquivo de config dependem do tipo de backend.
if [ "$BACKEND_KIND" = "functions" ]; then
  HOST_DIR="$WORKSPACE/src/${PROJECT_NAME}.Functions"
  CONFIG_FILE="$HOST_DIR/local.settings.json"
else
  HOST_DIR="$WORKSPACE/src/${PROJECT_NAME}.Api"
  CONFIG_FILE="$HOST_DIR/appsettings.Development.json"
fi

log() { echo "[post-create] $*"; }

# ─── 1. Connection string conforme o engine (hostname interno do compose) ───
SQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-Dev_Local_Pass_w0rd!}"
PG_PASSWORD="${POSTGRES_PASSWORD:-Dev_Local_Pass_w0rd!}"

case "$DB_ENGINE" in
  sqlserver)
    CONNECTION_STRING="Server=sqlserver,1433;Database=${PROJECT_NAME};User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True;"
    ;;
  postgres)
    CONNECTION_STRING="Host=postgres;Port=5432;Database=template;Username=app;Password=${PG_PASSWORD};"
    ;;
  none)
    CONNECTION_STRING=""
    ;;
  *)
    log "ERRO: DB_ENGINE desconhecido: $DB_ENGINE (use sqlserver | postgres | none)"; exit 1 ;;
esac

# Azurite: connection string de desenvolvimento explícita apontando para o serviço
# `azurite`. `UseDevelopmentStorage=true` assume 127.0.0.1 e NÃO acha o Azurite no compose.
AZURITE_CONN="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;"

# ─── 2. Restore de dependências (backend + frontend) ───
log "dotnet restore (solution)..."
dotnet restore "$WORKSPACE"

if [ "$USE_FRONTEND" = "true" ] && [ -d "$WEB_DIR" ]; then
  log "npm ci (web/)..."
  ( cd "$WEB_DIR" && npm ci )
fi

# ─── 3. Config local com secrets dummy (idempotente) ───
gen_secret() { openssl rand -base64 32; }

# Regenera se ausente OU (no caso functions) se estiver cifrado. Um local.settings.json
# com IsEncrypted:true vem do host (`func settings encrypt`); a chave do Data Protection
# é da máquina e não atravessa o boundary host↔container, então o func falha aqui dentro.
NEEDS_REGEN=0
if [ ! -f "$CONFIG_FILE" ]; then
  NEEDS_REGEN=1
elif [ "$BACKEND_KIND" = "functions" ] && \
     python3 -c "import json,sys; sys.exit(0 if json.load(open(sys.argv[1])).get('IsEncrypted') else 1)" "$CONFIG_FILE" 2>/dev/null; then
  log "local.settings.json está cifrado (gerado no host) — o container não descriptografa. Regenerando em texto plano..."
  rm -f "$CONFIG_FILE"
  NEEDS_REGEN=1
fi

if [ "$NEEDS_REGEN" -eq 1 ]; then
  mkdir -p "$(dirname "$CONFIG_FILE")"
  JWT_KEY="$(gen_secret)"
  log "Gerando $(basename "$CONFIG_FILE") com secrets dummy (backend=$BACKEND_KIND, db=$DB_ENGINE)..."

  if [ "$BACKEND_KIND" = "functions" ]; then
    # Formato Azure Functions: chaves em "Values" (flat, __ = nesting).
    python3 - "$CONFIG_FILE" "$CONNECTION_STRING" "$AZURITE_CONN" "$JWT_KEY" "$USE_AZURITE" <<'PY'
import json, sys
path, conn, azurite, jwt_key, use_azurite = sys.argv[1:6]
values = {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Jwt__Key": jwt_key,
    "Jwt__Issuer": "Template",
    "Jwt__Audience": "Template",
    "Jwt__ExpiresInMinutes": "60",
    "Cors__AllowedOrigins": "http://localhost:5173",
}
if use_azurite == "true":
    values["AzureWebJobsStorage"] = azurite
data = {"IsEncrypted": False, "Values": values}
if conn:
    data["ConnectionStrings"] = {"Default": conn}
with open(path, "w") as f:
    json.dump(data, f, indent=4); f.write("\n")
PY
  else
    # Formato ASP.NET Core: appsettings.Development.json (config aninhada).
    python3 - "$CONFIG_FILE" "$CONNECTION_STRING" "$JWT_KEY" <<'PY'
import json, sys
path, conn, jwt_key = sys.argv[1:4]
data = {
    "ConnectionStrings": {"Default": conn} if conn else {},
    "Jwt": {"Key": jwt_key, "Issuer": "Template", "Audience": "Template", "ExpiresInMinutes": "60"},
    "Cors": {"AllowedOrigins": "http://localhost:5173"},
}
with open(path, "w") as f:
    json.dump(data, f, indent=2); f.write("\n")
PY
  fi
  log "$(basename "$CONFIG_FILE") criado."
else
  log "$(basename "$CONFIG_FILE") já existe e está utilizável — preservado (idempotente)."
fi

# ─── 4. Allowlist do Claude no container (Claude sem prompts) ───
# Vive APENAS no container: copiada para o user settings (~/.claude/settings.json),
# NUNCA mergeada no .claude/settings.json versionado do host.
CLAUDE_ALLOWLIST="$WORKSPACE/.devcontainer/claude-settings.json"
if [ -f "$CLAUDE_ALLOWLIST" ]; then
  log "Instalando allowlist do Claude em ~/.claude/settings.json..."
  mkdir -p "$HOME/.claude"
  cp "$CLAUDE_ALLOWLIST" "$HOME/.claude/settings.json"
else
  log "AVISO: $CLAUDE_ALLOWLIST não encontrado — Claude pedirá permissões."
fi

# ─── 5. Migrations (espera o banco; retry mesmo com depends_on healthy) ───
if [ "$DB_ENGINE" = "none" ]; then
  log "DB_ENGINE=none — pulando migrations."
  log "Ambiente pronto."
  exit 0
fi

log "Restaurando ferramentas locais (.config/dotnet-tools.json → dotnet-ef)..."
( cd "$WORKSPACE" && dotnet tool restore )

log "Aplicando migrations (dotnet ef database update)..."
# CRÍTICO: em design-time o `dotnet ef` IGNORA o config do host e lê a connection string
# da env var ConnectionStrings__Default. Sem este export o EF tentaria localhost e não
# acharia o banco dentro do container.
export ConnectionStrings__Default="$CONNECTION_STRING"
for attempt in $(seq 1 10); do
  if ( cd "$WORKSPACE" && dotnet ef database update \
      --project "$INFRA_PROJECT" \
      --startup-project "$HOST_DIR" ); then
    log "Migrations aplicadas."
    break
  fi
  log "Banco ainda não pronto (tentativa $attempt/10); aguardando 6s..."
  sleep 6
  if [ "$attempt" -eq 10 ]; then
    log "ERRO: não foi possível aplicar migrations após 10 tentativas."
    exit 1
  fi
done

log "Ambiente pronto."
