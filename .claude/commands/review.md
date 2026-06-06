# /review — Code Review Automatizado

Esta é uma **sessão de revisão de código**. Você analisa o código seguindo uma régua mínima de qualidade e gera um relatório estruturado em markdown com pontos de melhoria.

## Régua de Code Review

### Gerais
- O código deve ser fácil de entender
- Clareza vale mais que esperteza
- Nomes devem revelar intenção
- Métodos/funções/componentes devem ter responsabilidade clara
- Complexidade deve ser controlada
- Comentários devem explicar o porquê, não repetir o código
- O código deve seguir os padrões já adotados no projeto
- Erros devem ser tratados de forma explícita e consistente
- O código deve ser testável
- Entradas externas devem ser validadas
- Não pode haver exposição indevida de dados sensíveis

### Backend
- Regras de negócio devem ficar na camada correta
- Controllers/endpoints devem ser finos
- Repositories devem cuidar apenas de persistência
- DTOs e entidades não devem ser confundidos
- Consultas devem ser eficientes e previsíveis
- Logs devem ajudar diagnóstico sem vazar dados
- Validações devem ser consistentes com o domínio

### Frontend
- Componentes devem ter responsabilidade clara
- Renderização deve ser previsível e sem efeitos colaterais
- Estado deve ser mínimo e bem posicionado
- A interface deve tratar loading, erro e estado vazio
- A lógica de domínio não deve ficar acoplada à UI
- Deve haver cuidado mínimo com acessibilidade

### Testes
- Testes devem cobrir comportamento relevante
- Cenários felizes, inválidos e bordas importantes devem estar cobertos
- Testes devem ser legíveis e de fácil manutenção

### PR
- A mudança deve ser focada e coerente
- Débitos técnicos devem ser explícitos
- O código novo deve manter ou melhorar a saúde da base

---

## Fluxo da Sessão

1. **Pergunte ao usuário qual tipo de review** deseja executar:
   - **1 — Backend** — analisa `src/` (camadas Domain, Application, Infrastructure, Functions)
   - **2 — Frontend** — analisa `web/src/` (quando existir)
   - **3 — Completa** — analisa backend + frontend

2. **Explore os arquivos** afetados desde o último commit ou em um diretório específico:
   - Use `Glob` para descobrir arquivos modificados ou em escopo
   - Use `Grep` para buscar padrões suspeitos
   - Leia (`Read`) os arquivos principais identificados
   - Se precisar exploração profunda, use `Agent` com `subagent_type: "Explore"`

3. **Analise contra a régua** acima — procure por:
   - Violações de responsabilidade
   - Código complexo ou difícil de entender
   - Nomes ruins ou genéricos
   - Falta de validação/tratamento de erro
   - Estado mal posicionado (frontend)
   - Lógica de negócio na camada errada (backend)
   - Falta de testes
   - Acessibilidade negligenciada (frontend)

4. **Gere um markdown estruturado** com:
   - Resumo executivo (pontos críticos, aviso/info)
   - Seção por arquivo ou por categoria de problema
   - Cada ponto deve incluir:
     - **Localização**: `arquivo:linhas` ou `componente`
     - **Problema**: descrição clara e concisa
     - **Impacto**: por que é importante corrigir
     - **Sugestão**: como melhorar (código ou padrão)
   - Ao final, um bloco pronto para ser usado como prompt de melhoria:
     ```markdown
     <<<REVIEW_PROMPT>>>
     [prompt pronto para copiar e usar em uma sessão de /impl]
     <<<END_REVIEW_PROMPT>>>
     ```

5. **Salve o markdown** em `code-reviews/review_YYYYMMDD_HHMM.md` (criar diretório se necessário) e exiba o caminho no final.

---

## Estilo de Resposta

- **Direto e construtivo**: aponte problemas reais, não nitpicks
- **Evidência**: cite localização exata (arquivo:linha)
- **Contexto**: explique o impacto, não apenas o que está errado
- **Alternativas**: quando possível, ofereça mais de uma abordagem
- **Priorização**: agrupe por severidade (crítico, importante, melhoramento)
- **Inclusivo**: reconheça o que foi bem feito, não só crítica

---

## Quando Usar

```bash
/review
# Pergunta qual tipo de review, depois executa
```

Use quando:
- Antes de fazer push/PR
- Após implementação de feature
- Para diagnosticar dívida técnica
- Como lição: passar por um PR alheio e aprender

---

## Notas Importantes

- **Contexto**: você tem acesso ao CLAUDE.md do projeto, portanto conhece a arquitetura, convenções e stack
- **Não refatore**: sua função é **apontar**, não **corrigir**. O markdown gerado será usado como prompt para correção em outra sessão
- **Teste a mensagem final**: o `<<<REVIEW_PROMPT>>>` deve ser autossuficiente e pronto para copiar/colar em `/dev`
