# Imagem do serviço `app` do Dev Container isolado do template-backend.
# Ver .devcontainer/README.md para o modelo de isolamento e como trocar de stack.
#
# Base: devcontainer .NET 9 (Debian 12, traz SDK 9.0 + usuário `vscode` uid 1000).
# Camadas OPCIONAIS são controladas por build args, para que cada SaaS instale
# apenas o que usa (uma API pura não precisa de func tools nem de Node):
#
#   INSTALL_NODE=true      → Node 24 LTS          (frontend Vite, ou tooling npm)
#   INSTALL_FUNC=true      → Azure Functions Core Tools v4 (host BACKEND_KIND=functions)
#   INSTALL_MSSQL=true     → mssql-tools18 / sqlcmd (DB_ENGINE=sqlserver)
#   INSTALL_PSQL=true      → cliente psql          (DB_ENGINE=postgres)
#
# O socket do Docker NÃO é montado (decisão de isolamento): sem docker-in-docker,
# sem mount de /var/run/docker.sock. O pior caso é estragar este container.

FROM mcr.microsoft.com/devcontainers/dotnet:9.0

ARG INSTALL_NODE=true
ARG INSTALL_FUNC=true
ARG INSTALL_MSSQL=true
ARG INSTALL_PSQL=false

# ─── Camadas root: pacotes de sistema, clientes de banco, ferramentas globais ───
USER root

# Pré-requisitos comuns para os repositórios externos (NodeSource, Microsoft).
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && mkdir -p /etc/apt/keyrings \
    && rm -rf /var/lib/apt/lists/*

# Node 24 LTS via NodeSource (Debian). Opcional: frontend Vite / tooling npm.
RUN if [ "$INSTALL_NODE" = "true" ]; then \
        curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
            | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
        && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_24.x nodistro main" \
            > /etc/apt/sources.list.d/nodesource.list \
        && apt-get update \
        && apt-get install -y --no-install-recommends nodejs \
        && rm -rf /var/lib/apt/lists/* ; \
    fi

# mssql-tools18 (sqlcmd) + driver ODBC, via repositório oficial da Microsoft.
# Necessário para DB_ENGINE=sqlserver (healthcheck, seed/diagnóstico manual via sqlcmd).
RUN if [ "$INSTALL_MSSQL" = "true" ]; then \
        curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
            | gpg --dearmor -o /etc/apt/keyrings/microsoft.gpg \
        && echo "deb [signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" \
            > /etc/apt/sources.list.d/mssql-release.list \
        && apt-get update \
        && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 mssql-tools18 unixodbc-dev \
        && rm -rf /var/lib/apt/lists/* \
        && echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' > /etc/profile.d/mssql-tools.sh ; \
    fi
# profile.d garante o PATH inclusive em login shells (`bash -lc` re-sourceia /etc/profile
# e descartaria um ENV PATH herdado). ENV cobre os shells não-login.
ENV PATH="${PATH}:/opt/mssql-tools18/bin"

# Cliente psql, via repositório de pacotes do .NET base (Debian). DB_ENGINE=postgres.
RUN if [ "$INSTALL_PSQL" = "true" ]; then \
        apt-get update \
        && apt-get install -y --no-install-recommends postgresql-client \
        && rm -rf /var/lib/apt/lists/* ; \
    fi

# Ferramentas globais npm: Azure Functions Core Tools v4 (só BACKEND_KIND=functions).
# `--unsafe-perm` é necessário para a compilação nativa do core tools rodando como root.
RUN if [ "$INSTALL_FUNC" = "true" ] && [ "$INSTALL_NODE" = "true" ]; then \
        npm install -g --unsafe-perm azure-functions-core-tools@4 ; \
    fi

# Claude Code: instalado via npm se Node estiver presente; caso contrário, via instalador
# nativo (curl) para que uma stack de API pura sem Node ainda tenha o Claude no container.
RUN if [ "$INSTALL_NODE" = "true" ]; then \
        npm install -g --unsafe-perm @anthropic-ai/claude-code ; \
    else \
        curl -fsSL https://claude.ai/install.sh | bash || true ; \
    fi

# ─── PATH de tools .NET globais do usuário `vscode` ───
# NÃO instalamos dotnet-ef global: o template fixa dotnet-ef como TOOL LOCAL
# (.config/dotnet-tools.json). O post-create.sh roda `dotnet tool restore` e
# `dotnet ef` resolve a local. Mantemos o dir de tools globais no PATH para
# qualquer ferramenta instalada ad-hoc na sessão.
RUN echo 'export PATH="$PATH:/home/vscode/.dotnet/tools"' > /etc/profile.d/dotnet-tools.sh
ENV PATH="${PATH}:/home/vscode/.dotnet/tools"

USER vscode
