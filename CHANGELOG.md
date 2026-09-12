# Changelog

Todas as mudanças notáveis deste framework são registradas aqui. Formato baseado em
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/); como este é um framework
interno sem releases versionadas publicamente, as entradas são organizadas por data.

## [Não lançado]

### Alterado
- **Consolidação dos slash commands de 10 para 6**, cada um com responsabilidade exclusiva e não sobreposta: `/analyst`, `/arquiteto`, `/flow`, `/spike`, `/qa`, `/infra`.
  - `/spike` absorveu `/dev` (implementação), `/bugfix` (correção) e `/review` (revisão de código) — passa a ser o único agente autorizado a tocar código de produto, operando em modos (investigação, implementação de epic, implementação direta, correção de bug, revisão).
  - `/flow` deixou de ser o orquestrador do ciclo completo (`analyst → spike → SM → dev → bugfix`) e passou a ser exclusivamente o mediador entre `/arquiteto` e `/analyst`: pareceres independentes, cruzamento, no máximo 1 rodada de contraposição, escalonamento ao usuário se o conflito persistir.
  - `/infra` passou a ser explicitamente o dono de tudo em `.claude/` (comandos, settings, sincronia canônico→raiz), além de ambiente de dev e automação.
  - `/qa` passou a ser explicitamente independente, orientado por epics/documentação/código, com autonomia total sobre massa de dados do banco local (nunca produção).
  - `/arquiteto` ganhou uma seção dedicada de segurança/DevSecOps (AuthN/AuthZ, dados sensíveis, superfície de ataque, CI/CD e supply chain, retenção/privacidade).
  - `/analyst` ganhou postura de compliance/jurídico (privacidade, retenção de dado, exposição legal) como parte da régua crítica.
- `/SM`, `/dev`, `/bugfix` e `/review` foram **removidos** de `.claude/commands/` (canônico em `template-backend/`, espelhado na raiz via `sync-commands.sh`).

### Adicionado
- Convenção de gestão de trabalho via `docs/epics/{backlog,em-andamento,finalizados}/` substituindo GitHub Issues/Projects como sistema de tracking — documentada em [`docs/template-usage.md`](docs/template-usage.md) e referenciada no `CLAUDE.md`.
- `template-backend/docs/README.md` e o esqueleto de pastas `docs/epics/` — já viajam prontos para todo novo produto criado a partir do template.

## [2026-06-06] — Dev Container isolado + slash commands iniciais

### Adicionado
- Dev Container parametrizável para `template-backend/` (Docker socket não montado, allowlist ampla isolada em `claude-settings.json`), com stacks alternáveis via fragmentos de `docker-compose` (SQL Server, PostgreSQL, Azurite) e `post-create.sh` idempotente.
- Primeira leva de slash commands em `.claude/commands/`: `/analyst`, `/arquiteto`, `/spike`, `/SM`, `/dev`, `/bugfix`, `/review`, `/qa`, `/infra`, `/flow`.
- `template-backend/scripts/sync-commands.sh` para manter a cópia canônica (`template-backend/.claude/commands/`) e o espelho da raiz (`.claude/commands/`) em sincronia.
- CI e quality checks de frontend como placeholders (`echo "TODO: ..."`) em `template-backend/.github/workflows/`.
- READMEs iniciais descrevendo o propósito de cada componente do template.
