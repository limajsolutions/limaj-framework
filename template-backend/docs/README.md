# {Produto} — Documentação

Este índice aponta para a documentação funcional e técnica alinhada ao código atual.
Ao copiar `template-backend/` para um produto novo, preencha as seções abaixo — o
esqueleto de pastas (`docs/epics/{backlog,em-andamento,finalizados}/`) já vem pronto.

## Índice

- [Introdução](./introduction.md) — *(criar)* visão geral do produto e do problema que resolve
- [Glossário](./glossary.md) — *(criar)* termos de domínio
- [Regras de Negócio](./business-rules.md) — *(criar)* mantido pelo `/analyst`
- Documentação técnica/arquitetural — mantida pelo `/arquiteto`, um `.md` por decisão relevante ou área

## Gerenciamento de Epics — `docs/epics/`

`docs/epics/` é o sistema de gestão de tarefas do produto — substitui um board externo
(GitHub Projects, Jira, etc.). Cada epic é um `.md` com checkboxes, criado a partir do
consenso entre `/analyst` e `/arquiteto` mediado pelo `/flow`, e implementado pelo `/spike`.

**Fluxo de pastas:** `backlog/` (formalizado, implementação não iniciada) → `em-andamento/`
(implementação em curso) → `finalizados/` (fases implementadas).

**Convenções obrigatórias** (replicadas do `CLAUDE.md` do produto — mantenha os dois em sincronia):
- `- [ ]` = pendente; `- [x]` = concluído
- Marcar `[x]` ao concluir etapa **antes** do commit
- Atualizar `Última revisão: YYYY-MM-DD` ao modificar
- Novo epic: `/flow` cria `docs/epics/backlog/{slug}.md` (sem entrada neste índice ainda)
- Ao iniciar a implementação, o `/spike` move de `backlog/` para `em-andamento/` e adiciona a
  linha correspondente abaixo, em "Epics em andamento"
- Ao concluir a última fase, o `/spike` move de `em-andamento/` para `finalizados/` e atualiza
  a linha correspondente para "Epics concluídos"
- **Mover para `finalizados/` é bookkeeping de implementação, não aprovação de negócio** —
  o sign-off funcional continua sendo decisão exclusiva do usuário

## Backlog

Epics formalizados via `/flow` mas com implementação ainda não iniciada — ver `docs/epics/backlog/`.
Sem entrada aqui até serem movidos para "Epics em andamento".

*(nenhum epic em backlog no momento)*

## Epics em andamento

*(nenhum epic em andamento no momento)*

## Epics concluídos

*(nenhum epic concluído no momento)*

## Roadmap

O roadmap é mantido nos documentos de epic em `docs/epics/`. Cada arquivo contém motivação,
decisões arquiteturais (`DA-###`), etapas com checkboxes e critérios de aceite. Esta
documentação descreve estado funcional implementado e contratos atuais.
