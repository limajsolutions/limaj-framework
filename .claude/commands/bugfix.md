Esta é uma **sessão de correção de bugs**.

Esta sessão opera em dois modos, detectados automaticamente pelo contexto fornecido pelo usuário:

- **Modo pós-implementação** — usuário cola o output de um script de automação ou lista de arquivos alterados + descreve o bug
- **Modo genérico** — usuário descreve um problema ou cola um erro sem contexto de implementação anterior

---

## Restrições de segurança obrigatórias

Antes de propor qualquer correção, verifique se ela não introduz ou silencia um problema de segurança. Se a correção tocar esses pontos, garanta que os invariantes abaixo estejam mantidos.

- [ ] **Ownership:** correções que tocam acesso a recurso por ID devem manter a verificação de propriedade do usuário. Cross-user retorna `NotFoundException` (HTTP 404) — nunca 403.
- [ ] **Input limits:** correções em services não removem validações de tamanho máximo de campos de texto.
- [ ] **Anti-overposting:** correções em DTOs não reintroduzem `IsActive` ou campos de controle interno em requests de atualização.
- [ ] **Rate limiting:** correções no fluxo de autenticação não contornam nem desativam mecanismos de rate limit.
- [ ] **Secrets hygiene:** a correção não expõe segredos em código, comentários ou logs.

---

## Protocolo de classificação de falha de teste (gate obrigatório)

> **Proibição absoluta: nenhum teste de regra de negócio pode ser alterado sem classificação explícita documentada no mesmo turno.**

Quando `dotnet test` falhar em um **teste existente**, siga este protocolo antes de qualquer ação:

### Passo 1 — Identificar e transcrever

Para cada teste que falhou, transcreva:
- **Nome do teste** (= contrato da regra)
- **Comportamento esperado** pelo teste (regra atual)
- **Comportamento que a correção produz** (regra proposta)

### Passo 2 — Classificar a falha

| Categoria | Definição | Ação obrigatória |
|---|---|---|
| **A — Regressão real** | O código corrigido quebrou um comportamento que deveria continuar funcionando | Ajustar a correção. **Não tocar no teste.** |
| **B — Regra mudou** | A correção altera intencionalmente o comportamento que o teste cobre | **NÃO alterar o teste ainda.** Apresentar ao usuário com a pergunta: _"A regra [X] mudou de [comportamento anterior] para [comportamento novo]. Confirma a mudança?"_ Só após aval explícito: atualizar o teste e registrar "regra anterior vs. regra nova" na mensagem de commit. |
| **C — Flaky/infra** | Falha não relacionada à lógica (timeout, dependência de ambiente, ordem de execução) | Isolar e registrar. Não mascarar ajustando assertions. |

### Passo 3 — Documentar a classificação

Antes de qualquer edição em teste existente, exiba no turno:

```
## Classificação de falha de teste

**Teste:** NomeDoTeste
**Categoria:** A / B / C — [justificativa em 1 frase]
**Ação:** [o que será feito]
```

Teste alterado sem esse bloco = maquiagem de regressão.

---

## O que fazer ao iniciar

Peça ao usuário:

> **Forneça:**
> - **Contexto do problema** — pode ser:
>   - Lista de arquivos alterados + descrição do bug
>   - Descrição direta do problema, stack trace, comportamento observado vs. esperado, ou trecho de código suspeito
> - Quanto mais contexto, mais preciso o diagnóstico

---

## Modo pós-implementação

Ativado quando o usuário colar lista de arquivos alterados e/ou bloco de commit message.

### Etapa 1: Extrair contexto
1. Localize a lista de arquivos alterados
2. Extraia o número da issue (se mencionado)
3. Identifique todos os arquivos `.cs`, `.ts`, `.tsx`, `.md` que foram tocados

### Etapa 2: Diagnosticar
1. Leia TODOS os arquivos alterados antes de fazer qualquer mudança
2. Examine o contexto completo de cada arquivo
3. Compare com o código vizinho para entender a regra de negócio afetada
4. **Descreva sua hipótese de causa raiz** (1-2 frases) antes de corrigir
   - O que está errado no código atual
   - Por que isso causa o bug descrito

### Etapa 3: Corrigir
Implemente a correção imediatamente — o contexto já está estabelecido.

### Etapa 4: Entregar
```
## Diagnóstico

[Causa raiz em 1-2 frases diretas]

## Arquivos corrigidos

- [arquivo1.cs](path)
- [arquivo2.ts](path)

## Mensagem de commit

<<<COMMIT>>>
fix(escopo): corrigir [o que foi corrigido]
<<<END_COMMIT>>>
```

---

## Modo genérico

Ativado quando o usuário descreve um problema sem lista de arquivos.

### Etapa 1: Entender o contexto
1. Leia os arquivos relevantes mencionados ou inferidos pelo erro
2. Se o erro não aponta arquivos diretamente, busque por símbolos, rotas ou mensagens de erro no codebase

### Etapa 2: Diagnosticar
1. Analise o erro ou comportamento descrito
2. Trace o caminho de execução até o ponto de falha
3. Formule a hipótese de causa raiz com evidências (arquivo:linha)

### Etapa 3: Apresentar e perguntar
Exiba o diagnóstico estruturado e **pergunte se pode prosseguir com a correção**:

```
## Diagnóstico

**Causa raiz:** [descrição direta em 1-2 frases]

**Evidências:**
- [arquivo.cs:42](path) — [o que está errado aqui]
- [outro.ts:17](path) — [o que está errado aqui]

**Correção proposta:** [o que será alterado, em termos concretos]

---
Posso aplicar essa correção?
```

### Etapa 4: Corrigir (após confirmação)
1. Implemente a correção **mínima e cirúrgica**
2. Não faça refatorações nem limpezas além do escopo
3. Entregue a mensagem de commit no formato padrão

---

## O que NÃO fazer

- Não implementar features ou refatorações além da correção do bug
- Não fazer commit — apenas gere a mensagem pronta
- Não mover issues no GitHub Projects
- Não adicionar comentários, docstrings ou type annotations em código não alterado
- Não alterar estrutura de pastas ou reorganizar código

---

## Convenções obrigatórias

Siga as convenções do `CLAUDE.md` do projeto:

- **Formato de commit**: `fix(escopo): descrição breve (#NNN)` — incluir issue se houver
- **Camadas**: Domain, Application, Infrastructure, Functions
- **Erros**: use `DomainValidationException`, `NotFoundException`, `ConflictException` de `Limaj.Framework.Abstractions`
- **Datas**: `DateOnly` para datas sem hora; `DateTime` apenas para timestamps de auditoria
