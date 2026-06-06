Esta é uma **sessão de implementação**. Você recebe qualquer tipo de entrada e implementa — sem depender de outro comando para começar.

> **Ao abrir a sessão sem entrada:** responda apenas "Sessão de implementação ativa. Aguardando seu prompt." e pare. Não leia arquivos, não faça varredura do projeto, não antecipe nada. Espere o usuário enviar a tarefa.

## Fontes de entrada aceitas

| Origem | O que fazer |
|---|---|
| **Prompt direto do usuário** | Leia, entenda o objetivo e implemente |
| **Arquivo de documentação** (`/Docs/...`, spec, ADR) | Leia o arquivo indicado, extraia os requisitos e implemente |
| **Prompt funcional gerado pelo `/SM`** | Trate como qualquer prompt — leia e implemente |

Nunca pergunte de onde veio o input nem exija um formato específico. Se a intenção estiver clara, implemente. Se houver ambiguidade real de escopo, faça **uma** pergunta objetiva antes de prosseguir.

## Restrições de segurança obrigatórias

Antes de propor qualquer implementação, verifique se os itens abaixo estão cobertos. Se a feature tocar esses pontos e a proposta os ignorar, corrija antes de prosseguir.

- [ ] **Ownership:** todo acesso a recurso por ID verifica propriedade do usuário. Cross-user retorna `NotFoundException` (HTTP 404) — nunca 403.
- [ ] **Input limits:** novos campos de texto têm limite máximo definido no service. Lançar `DomainValidationException` quando excedido.
- [ ] **Anti-overposting:** DTOs de atualização não expõem `IsActive` nem campos de controle interno. Mudanças de estado sensíveis usam operações dedicadas.
- [ ] **Rate limiting:** endpoints de autenticação e operações críticas consideram mecanismos de rate limit. Retornar `ErrorType.TooManyRequests` → HTTP 429 quando aplicável.
- [ ] **Secrets hygiene:** nenhum segredo em código, comentários ou logs. Usar variáveis de ambiente / App Settings.

## Regra obrigatória de testes unitários

> **Esta regra não tem exceções. Toda alteração de comportamento deve ter cobertura de teste.**

| Situação | Obrigação |
|---|---|
| Novo código criado (service, método, lógica) | Criar testes unitários que cubram o comportamento introduzido |
| Comportamento removido | Remover ou reescrever os testes vinculados para cobrir o novo comportamento esperado |
| Comportamento modificado | Modificar os testes existentes e/ou criar novos para refletir o novo comportamento |

**Regra geral:** se você escreveu ou alterou código de produto e não tocou em nenhum arquivo de teste, a implementação está incompleta.

Siga os padrões de `TestBase`, `AutoFixture` e `Moq` já presentes no projeto.

## Protocolo de classificação de falha de teste (gate obrigatório)

> **Proibição absoluta: nenhum teste de regra de negócio pode ser alterado sem classificação explícita documentada no mesmo turno.**

Quando `dotnet test` falhar em um **teste existente**, siga este protocolo antes de qualquer ação:

### Passo 1 — Identificar e transcrever

Para cada teste que falhou, transcreva:
- **Nome do teste** (= contrato da regra)
- **Comportamento esperado** pelo teste (regra atual)
- **Comportamento que o novo código produz** (regra proposta)

### Passo 2 — Classificar a falha

| Categoria | Definição | Ação obrigatória |
|---|---|---|
| **A — Regressão real** | O novo código quebrou um comportamento que deveria continuar funcionando | Corrigir o código. **Não tocar no teste.** |
| **B — Regra mudou** | A feature altera intencionalmente o comportamento que o teste cobre | **NÃO alterar o teste ainda.** Apresentar ao usuário com a pergunta: _"A regra [X] mudou de [comportamento anterior] para [comportamento novo]. Confirma a mudança?"_ Só após aval explícito: atualizar o teste e registrar "regra anterior vs. regra nova" na mensagem de commit. |
| **C — Flaky/infra** | Falha não relacionada à lógica (timeout, dependência de ambiente, ordem de execução) | Isolar e registrar. Não mascarar ajustando assertions. |

### Passo 3 — Documentar a classificação

Antes de qualquer edição, exiba no turno:

```
## Classificação de falha de teste

**Teste:** NomeDoTeste
**Categoria:** A / B / C — [justificativa em 1 frase]
**Ação:** [o que será feito]
```

Teste alterado sem esse bloco = maquiagem de regressão.

---

## O que fazer ao receber a entrada

1. Leia os arquivos relevantes antes de qualquer alteração — incluindo o arquivo de documentação se a entrada for um caminho
2. Implemente a feature completa conforme o objetivo descrito, seguindo as convenções do projeto (CLAUDE.md)
3. Crie ou atualize os testes unitários correspondentes às mudanças realizadas (ver regra acima)
4. Execute `dotnet format` na raiz do projeto e corrija qualquer aviso de formatação antes de prosseguir
5. Execute `dotnet test` (ou o projeto de testes afetado) e corrija qualquer falha causada pelas mudanças antes de prosseguir
6. Ao final, gere a mensagem de commit em inglês no formato convencional (`feat:`, `fix:`, etc.)
7. Inclua uma seção **"O que isso resolve"** em linguagem direta explicando o impacto prático

## Convenções de nomenclatura (obrigatórias)

Ao escrever ou alterar código, o nome de variáveis e métodos deve descrever **o comportamento real**, não a expectativa do chamador.

| Padrão a evitar | Substituto correto | Motivo |
|---|---|---|
| `currentX` quando X depende de um parâmetro de data | `xAt` ou `xByDate` | "current" implica "agora"; se recebe uma data, o nome deve dizer isso |
| `GetCurrentX(date)` | `GetXAt(date)` ou `GetXByDateAsync(date)` | Mesmo princípio para métodos |
| `result`, `data`, `value` como nomes de variável locais | Nome semântico do que o valor representa | Genérico demais para debug |
| Abreviações não universais | Nome completo | Clareza na leitura |

**Regra geral:** se remover o nome e substituir por uma descrição de uma frase, essa frase deve ser mais longa do que o nome. Se o nome já diz exatamente o que o valor é, está correto.

## O que NÃO fazer

- Não pergunte se deve começar — ao receber uma tarefa, implemente diretamente sem pedir confirmação
- Não faça commit — apenas gere a mensagem pronta para o usuário executar
- Não mova issues no GitHub Projects
- Não adicione features além do especificado no prompt
- Não adicione comentários, docstrings ou tipagens em código que não foi alterado
