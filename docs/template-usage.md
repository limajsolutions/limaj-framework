# Uso do template-backend

Guia de como consumir e evoluir `template-backend/` — o gabarito copiado para iniciar um
novo produto SaaS (ver passo a passo completo no [README da raiz](../README.md)).

## Slash commands do produto

`template-backend/.claude/commands/` traz 6 comandos, cada um com responsabilidade
exclusiva e não sobreposta:

| Comando | Papel |
|---|---|
| `/analyst` | Regras funcionais de negócio, edge cases e riscos jurídicos/compliance de uma proposta |
| `/arquiteto` | Desenho técnico, stack, TDD, segurança/DevSecOps — só produz Markdown |
| `/flow` | Media a discussão entre `/arquiteto` e `/analyst` até consenso; formaliza o epic |
| `/spike` | Único agente que altera código de produto — investiga, implementa, corrige bugs, revisa |
| `/qa` | Execução independente de testes, orientada por epics/documentação/código |
| `/infra` | Automação, ambiente de dev local e manutenção de `.claude/` |

Ver a descrição completa de cada papel no [`CLAUDE.md`](../CLAUDE.md) da raiz (seção
"Prompt / command surfaces") e o conteúdo de cada arquivo em `template-backend/.claude/commands/`.

## Gestão de trabalho: epics em `docs/epics/`

O produto gerado a partir do template **não usa um board externo** (GitHub Projects, Jira)
como sistema de gestão de tarefas — usa `docs/epics/`, versionado junto com o código.
`template-backend/docs/` já vem com o esqueleto pronto:

```
docs/
  README.md              # índice funcional/técnico + listas de epics por status
  epics/
    backlog/              # formalizado pelo /flow, implementação não iniciada
    em-andamento/          # implementação em curso (movido pelo /spike)
    finalizados/           # fases implementadas (movido pelo /spike)
```

**Ciclo de um epic:**

1. O desenvolvedor traz uma ideia/dúvida/bug ao `/flow`.
2. `/flow` colhe pareceres independentes do `/arquiteto` e do `/analyst`, cruza os dois e
   busca consenso (no máximo 1 rodada de contraposição; conflito real sobe para o
   desenvolvedor decidir).
3. Se o ponto vira trabalho concreto, `/flow` escreve `docs/epics/backlog/{slug}.md` —
   contexto, decisões arquiteturais (`DA-###`), estrutura por camada e checklist de fases
   com `- [ ]`.
4. O desenvolvedor aciona o `/spike` apontando o epic. Ao iniciar, o `/spike` move o arquivo
   para `docs/epics/em-andamento/` e adiciona a entrada em `docs/README.md`.
5. `/spike` implementa fase a fase, marcando `- [x]` a cada item concluído, sempre com
   testes e (se houver mudança de schema) migration aplicada no mesmo turno.
6. Ao concluir a última fase, `/spike` move o epic para `docs/epics/finalizados/` e atualiza
   `docs/README.md`. Isso é bookkeeping de implementação — **não** é aprovação de negócio;
   o sign-off funcional continua sendo do usuário.
7. `/qa` valida o epic concluído usando os critérios de aceite do próprio arquivo como
   roteiro de teste.

Esta convenção é baseada em uso real em produtos já construídos a partir deste framework —
ver `bootstrap/allowlist.md` para o que foi importado do repositório legado de origem.

## O que falta documentar aqui

- `template-backend/src/` e `template-backend/test/` ainda são READMEs placeholder — ver
  nota de honestidade no [README da raiz](../README.md#iniciar-um-novo-saas-a-partir-deste-gabarito-passo-a-passo).
- Convenções de teste (xUnit/Moq/AutoFixture) ficam a cargo do `/arquiteto` de cada produto,
  documentadas no `CLAUDE.md` do produto quando a stack for definida.
