# Limaj Framework

Base reutilizável para acelerar criação de backends SaaS em .NET/Azure Functions, com foco em:

- building blocks técnicos genéricos (`packages/Limaj.Framework.*`)
- template de projeto backend (`template-backend/`), com Dev Container isolado para o Claude Code
- slash commands do Claude Code (`.claude/commands/`) carregados como sessões especializadas

## Estrutura do repositório

- `packages/Limaj.Framework.Abstractions`: contratos, tipos comuns e erros base.
- `packages/Limaj.Framework.Application`: camada de aplicação desacoplada de infraestrutura concreta.
- `packages/Limaj.Framework.Persistence.EFCore`: adapters EF Core que implementam contratos das abstrações.
- `packages/Limaj.Framework.Functions`: pipeline HTTP genérico para host Functions.
- `template-backend/`: gabarito para iniciar novos produtos — já traz `.claude/commands/`, `docs/epics/{backlog,em-andamento,finalizados}/` e `docs/README.md` prontos.
- `docs/template-usage.md`: como consumir/evoluir o template, incluindo o ciclo de gestão de epics em `docs/epics/`.
- `Limaj.Framework.sln`: solução do framework para build/test dos pacotes base.

## Iniciar um novo SaaS a partir deste gabarito (passo a passo)

Guia executável de ponta a ponta. Siga na ordem; cada bloco é copiável. O exemplo
usa o nome de produto **`Acme`** — troque por `MeuSaas` à vontade.

> **Pré-requisitos:** `git`, `docker`, `dotnet` SDK 9, VS Code com a extensão
> **Dev Containers**, e a CLI `gh` (opcional, só para criar o repo no GitHub).
>
> **Nota de honestidade:** hoje `template-backend/src/` e `template-backend/test/`
> contêm apenas READMEs placeholder — **não há `.csproj`/`.sln` prontos**. Os passos 4–5
> criam a estrutura .NET real. Quando o template passar a trazer projetos prontos, os
> passos 4–5 viram apenas "renomear".

### 1. Criar o repositório do produto a partir do template

Copie **só** o conteúdo de `template-backend/` para um repositório novo (ele já traz
`.claude/commands/`, `.devcontainer/`, `.vscode/`, `src/`, `test/`, `.github/`):

```bash
# defina o nome do produto (sem espaços; usado em pastas e namespaces)
PRODUCT=Acme

# clone o framework e copie o gabarito para a pasta do novo produto
git clone https://github.com/thalleslima8/limaj-framework.git
cp -r limaj-framework/template-backend "$PRODUCT"
cd "$PRODUCT"

# inicialize o repo do produto do zero (sem o histórico do framework)
rm -rf .git
git init -b main
```

### 2. Renomear `Template` → nome do produto

Troca em massa de pastas, arquivos e conteúdo. Trata **as duas caixas**: `Template`
(namespaces, projetos) → `Acme`, e `template` minúsculo (rede/volumes/container_name do
Docker, database name) → `acme` — senão dois produtos colidiriam na mesma rede/volume
Docker. Rode na raiz do repo do produto:

```bash
PRODUCT=Acme                                          # o mesmo nome do passo 1
PRODUCT_LC=$(echo "$PRODUCT" | tr '[:upper:]' '[:lower:]')

# 2a. conteúdo dos arquivos (namespaces, identificadores Docker, paths em scripts/configs)
grep -rl --binary-files=without-match -i "template" . \
  | grep -v '/.git/' \
  | xargs -r sed -i -e "s/Template/$PRODUCT/g" -e "s/template/$PRODUCT_LC/g"

# 2b. nomes de pastas e arquivos (de baixo para cima: filhos antes dos pais)
find . -depth -iname '*template*' -not -path './.git/*' | while read -r p; do
  newbase=$(basename "$p" | sed -e "s/Template/$PRODUCT/g" -e "s/template/$PRODUCT_LC/g")
  mv "$p" "$(dirname "$p")/$newbase"
done
```

Confira que não sobrou nada (deve retornar vazio):

```bash
grep -rln -i "template" . | grep -v '/.git/'
```

### 3. Escolher o stack do Dev Container

Edite o stack do ambiente — **sem reescrever o container**. Detalhes e tabelas no
guia do devcontainer ([`.devcontainer/README.md`](template-backend/.devcontainer/README.md)
no produto; seção "Trocar de stack"). Resumo:

| Decisão | Onde mexer |
|---|---|
| Banco SQL Server (default) ↔ PostgreSQL | `dockerComposeFile` em `.devcontainer/devcontainer.json` (troque `compose.sqlserver.yml` por `compose.postgres.yml`) + `DB_ENGINE` e build args em `.devcontainer/docker-compose.yml` |
| Backend Functions (default) ↔ API Kestrel | `BACKEND_KIND` em `.devcontainer/docker-compose.yml` |
| Storage Azure (Azurite) | inclua/remova `compose.azurite.yml` + `USE_AZURITE` |
| Frontend (Node + Vite) | `USE_FRONTEND` + `INSTALL_NODE` |

Para uma **API + PostgreSQL sem frontend**, por exemplo: use `compose.postgres.yml`,
`DB_ENGINE=postgres`, `BACKEND_KIND=api`, `USE_AZURITE=false`, `USE_FRONTEND=false`,
e nos build args `INSTALL_MSSQL=false INSTALL_PSQL=true INSTALL_FUNC=false`.

### 4. Criar a solution e os projetos .NET

O template ainda não traz `.csproj`/`.sln` — crie-os agora (ajuste à sua escolha do passo 3).
Exemplo para o stack default (Functions + SQL Server):

```bash
PRODUCT=Acme
dotnet new sln -n "$PRODUCT"

# referência aos building blocks do framework (ProjectReference local ao clone).
# Ajuste o caminho ../limaj-framework se você moveu o clone.
FW=../limaj-framework/packages

# crie os projetos reais dentro das pastas já renomeadas em src/
# (substitua os READMEs placeholder pela estrutura .NET)
# -> use os templates que fizerem sentido: classlib p/ Domain/Application/Infrastructure,
#    func p/ Functions (ou webapi se BACKEND_KIND=api).
```

> Esta etapa depende das suas escolhas e dos templates `dotnet new` instalados. Use o
> agente **`/arquiteto`** (passo 7) para gerar a estrutura concreta de projetos e as
> `ProjectReference` corretas para `$FW/Limaj.Framework.*`, respeitando as fronteiras
> da seção [Fronteiras arquiteturais](#fronteiras-arquiteturais).

### 5. Fixar a ferramenta `dotnet-ef` (necessária para migrations no Dev Container)

O `post-create.sh` roda `dotnet tool restore` esperando `dotnet-ef` como **tool local**.
Crie o manifest uma vez:

```bash
dotnet new tool-manifest          # cria .config/dotnet-tools.json
dotnet tool install dotnet-ef     # fixa a versão usada pelo time
```

### 6. Abrir no Dev Container

```bash
code .
```

No VS Code: **Reopen in Container**. O primeiro build baixa a imagem e roda o
`post-create.sh` (restore, secrets dummy, config local, migrations). Ao terminar, rode
`claude` no terminal integrado — **sem nenhuma flag de permissão** (a allowlist ampla
vive só dentro do container; ver [guia do devcontainer](template-backend/.devcontainer/README.md)).

### 7. Usar os slash commands do Claude

Os comandos já vêm em `.claude/commands/` do produto — 6 comandos, cada um com uma
responsabilidade exclusiva (ver detalhamento no [`CLAUDE.md`](CLAUDE.md#prompt--command-surfaces)):

| Comando | Para quê |
|---|---|
| `/analyst` | regras funcionais de negócio, edge cases e riscos jurídicos/compliance → rascunho de epic |
| `/arquiteto` | arquitetura, stack, TDD e segurança/DevSecOps — só produz Markdown |
| `/flow` | media a discussão entre `/arquiteto` e `/analyst` até consenso; formaliza o epic em `docs/epics/` |
| `/spike` | único agente que altera código de produto — investiga, implementa, corrige bugs e revisa |
| `/qa` | testa o app (Playwright MCP, se houver `web/`) de forma independente, orientado por epics/docs/código |
| `/infra` | manter scripts, devcontainer, tooling e o próprio `.claude/` |

A gestão de trabalho é feita em `docs/epics/` (não em um board externo) — ver
[`docs/template-usage.md`](docs/template-usage.md) para o ciclo completo de um epic.

### 8. Validar e fazer o primeiro commit

```bash
dotnet build                       # build da solution do produto
dotnet test                        # quando houver projetos de teste
git add -A && git commit -m "chore: scaffold inicial a partir do limaj-framework"

# (opcional) publicar no GitHub
gh repo create "$PRODUCT" --private --source=. --push
```

## Fronteiras arquiteturais

Direção de dependência permitida (não quebrar):

1. `Abstractions` → sem dependências internas.
2. `Application` → apenas `Abstractions`.
3. `Persistence.EFCore` → `Abstractions`.
4. `Functions` (ou `Api`) → `Abstractions` + stack web/functions.

`Application` nunca referencia persistência concreta, identidade do host ou
infraestrutura. Se precisar de implementação concreta, defina o contrato em
`Abstractions` e ponha o adapter na camada de infraestrutura.

Building blocks a reaproveitar (não reinventar): `Result`/`Error` para fluxo de retorno,
contratos de persistência para os services, e o pipeline HTTP genérico (`FunctionRunner`)
para endpoints. Ver [CLAUDE.md](CLAUDE.md) para o catálogo completo de padrões.

## Manter os comandos em sincronia (só ao trabalhar NESTE repo)

A cópia canônica dos comandos é `template-backend/.claude/commands/` (é o que viaja para
cada SaaS). A raiz do framework tem um espelho para os comandos ficarem ativos ao abrir o
limaj. Após editar a canônica:

```bash
bash template-backend/scripts/sync-commands.sh        # espelha template → raiz
bash template-backend/scripts/sync-commands.sh --check # só verifica drift (CI)
```

## Validar o framework em si

```bash
dotnet build Limaj.Framework.sln
```

## Guardrails

- Não introduzir código de domínio específico dentro do framework.
- Não adicionar seed de dados de produto no framework base.
- Se precisar de implementação concreta, criar contrato em abstrações e adapter na camada de infraestrutura.
