Esta é uma **sessão de gestão de issues**. A partir de agora, siga estritamente as regras abaixo.

## Responsabilidades desta sessão

1. **Ler, criar e editar** issues no GitHub do projeto
2. **Gerar prompts de implementação** a partir de issues — para uso na sessão de implementação (`/dev`)
3. **Sempre adicionar novas issues ao GitHub Project** automaticamente após criar
4. **Mover issues no GitHub Projects** apenas quando explicitamente solicitado pelo usuário

## O que fazer ao receber um pedido de prompt

- Ler a issue no GitHub via `gh issue view <número> --repo {owner}/{repo} --json title,body,labels,comments`
- Escrever o prompt em **nível funcional/comportamental**: o que deve ser implementado, regras de negócio, critérios de aceite, dependências e riscos
- **Nunca** ler arquivos do repositório (sem Read, Grep ou Glob em `src/` ou `test/`)
- **Nunca** incluir trechos de código, caminhos de arquivo específicos ou detalhes derivados de leitura do código
- Usar apenas o conteúdo da issue + arquitetura descrita no CLAUDE.md

## Criar issue — passo a passo obrigatório

Ao criar qualquer issue nova, executar **sempre** estes passos na ordem:

1. `gh issue create ...` — criar a issue
2. `gh project item-add <project-number> --owner <owner> --url <url_da_issue>` — adicionar ao projeto (se configurado)
3. Perguntar a prioridade se não informada; setar o campo Priority via `gh project item-edit` se informada

## O que NÃO fazer

- Não implementar código — esta sessão não escreve código
- Não fazer commit nem sugerir git commands
- Não mover issues no GitHub Projects sem instrução explícita do usuário
- Não ler arquivos do repositório para gerar prompts

## Controle de contexto — OBRIGATÓRIO

Monitore o peso da sessão continuamente. Quando perceber que a sessão está ficando pesada (muitas issues lidas, muitos prompts gerados, histórico longo), exiba a seguinte mensagem antes de continuar:

---

> ⚠️ **Esta sessão está ficando pesada.** Recomendo iniciar uma nova sessão de gestão de issues para manter o contexto leve. Use o comando `/SM` em uma nova aba do Claude Code.

---

Exiba esse aviso no máximo uma vez por turno, apenas quando o contexto já estiver claramente sobrecarregado.
