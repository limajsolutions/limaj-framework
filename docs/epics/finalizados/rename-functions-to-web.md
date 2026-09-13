# Renomear Limaj.Framework.Functions para Limaj.Framework.Web

**Status:** Implementação concluída — pendente de fechamento de negócio pelo usuário
**Última revisão:** 2026-09-13

## Contexto

Uma investigação técnica (`/spike`, modo investigação) comparou `packages/Limaj.Framework.*`
com um produto real construído de forma independente (`FinanceFlow`), confirmando que
nenhum `.csproj` do FinanceFlow referencia `Limaj.Framework.*` — é uma reimplementação
paralela. Essa comparação revelou, entre outros pontos, que `Limaj.Framework.Functions`
(`FunctionRunner`, `ResultExtensions`, `ExceptionExtensions`) **não contém nenhuma linha
acoplada a Azure Functions** — usa exclusivamente `Microsoft.AspNetCore.Http.HttpRequest`/
`IResult`, os mesmos tipos que o Minimal API do ASP.NET Core usa nativamente. O nome do
pacote hoje sugere um acoplamento que não existe no código.

`/arquiteto` e `/analyst` foram consultados via `/flow` sobre uma proposta mais ampla
(renomear + generalizar para múltiplos hosts HTTP + embutir vertical slicing via classe
base). Após pareceres independentes e uma rodada de contraposição, chegaram a consenso
**parcial**: o rename é aprovado e faz sentido isolado (ver DA-001); a generalização para
suportar Minimal API como segundo host não exige trabalho novo (o contrato já é
agnóstico); e embutir vertical slicing via `IEndpoint`/`MapEndpoints` foi explicitamente
**excluído do escopo** deste epic (ver DA-003 — decisão fechada de não fazer agora, com
gatilho definido para reabrir).

## Decisões arquiteturais

- **DA-001 — Rename, não split de pacote.** `Limaj.Framework.Functions` vira
  `Limaj.Framework.Web`, mantendo a mesma dependência (`Abstractions` +
  `FrameworkReference Microsoft.AspNetCore.App`). Não há código específico de Azure
  Functions para extrair como adapter separado hoje — o binding de trigger
  (`[Function("X")]`) sempre foi e continua sendo código de produto, não framework.
  Um pacote adapter separado (`Limaj.Framework.Web.AzureFunctions`) só se justifica se
  algum produto precisar do binding não-ASP.NET-Core do Worker (`HttpRequestData`/
  `HttpResponseData`) — sem evidência disso hoje, não implementar preventivamente.

- **DA-002 — Vocabulário neutro de host.** Parâmetros/mensagens que hoje usam vocabulário
  de Azure Functions (`functionName`, `"in {Function}"` nos logs) passam a usar
  vocabulário agnóstico de host (`operationName` ou equivalente), já que o pacote se
  pretende utilizável por qualquer host que fale `HttpRequest`/`IResult`.

- **DA-003 — Vertical slicing (`IEndpoint`/`MapEndpoints`) fica fora deste epic.**
  Depois de uma rodada de contraposição, arquiteto e analyst convergiram em **especificar
  o desenho, mas não implementar em `packages/` ainda**: o contrato proposto
  (`void MapEndpoint(IEndpointRouteBuilder app)`, registro via `MapEndpoints(assembly)`
  restrito a uma assembly explícita, nunca scan global) resolve os requisitos de não
  presumir "1 endpoint = 1 recurso de domínio" e de ter via de escape (quem não usa
  Minimal API nunca referencia o tipo). Mesmo assim, fica de fora porque **nenhum produto
  real usa Minimal API hoje** — publicar essa API pública sem um consumidor real para
  validar a forma do contrato é o tipo de generalização especulativa que o `CLAUDE.md`
  pede para evitar em `packages/Limaj.Framework.*`. Ponto em aberto para quando isso for
  retomado: onde `IEndpoint` deveria morar fisicamente para não poluir a superfície
  pública de quem só usa Azure Functions (namespace/pacote isolado dentro de `Web`).
  **Gatilho para reabrir:** primeiro produto real que decidir expor endpoints via Minimal
  API — nesse momento, extrair o contrato já validado contra os casos reais que aparecerem
  (bulk, webhook multi-agregado, relatório cross-domain), em vez de projetá-lo às cegas
  agora.

- **DA-004 — Correção de segurança colateral, no mesmo PR.** `ExceptionExtensions`/
  `ResultExtensions.MapError`, no ramo de erro inesperado (500), hoje devolvem
  `ex.Message` cru no corpo da resposta HTTP (`Results.Problem(title: error.Message, ...)`)
  — vazamento de detalhe interno (mensagens de EF Core, stack de terceiros) para o
  cliente. Corrigir para: logar `ex.Message` completo via `ILogger` (já faz isso hoje),
  mas devolver ao cliente uma mensagem genérica fixa ("An unexpected error occurred.")
  sempre que não estiver em ambiente de desenvolvimento.

## Estrutura proposta por camada

```
packages/Limaj.Framework.Web/                    (rename de Limaj.Framework.Functions)
  Limaj.Framework.Web.csproj                      (mesmas deps: Abstractions + FrameworkReference AspNetCore.App)
  src/
    Http/
      RequestRunner.cs        (ex-FunctionRunner.cs; parâmetro "functionName" -> "operationName")
      ResultExtensions.cs      (lógica inalterada; ajuste de vocabulário só onde houver referência a "function")
      ExceptionExtensions.cs   (ajuste de vocabulário + fix DA-004)
```

Impacto em outros arquivos do repo (fora de `packages/`, mas parte da mesma entrega):
- `CLAUDE.md` — tabela de camadas: `Limaj.Framework.Functions` → `Limaj.Framework.Web`
  (dependências permitidas não mudam); seção "Exception bridge" atualiza os nomes de tipo/arquivo.
- `README.md` — item de estrutura do repositório que descreve `Limaj.Framework.Functions`.
- Qualquer consumidor existente do pacote (hoje, potencialmente `FinanceFlow` ou
  `template-backend`, se já copiado) precisa atualizar `PackageReference`/`ProjectReference`
  e `using Limaj.Framework.Functions.Http` → `Limaj.Framework.Web.Http`.

## Estratégia de testes

> **Nota de execução (2026-09-13):** o texto abaixo, como escrito originalmente, presumia que
> este epic criaria "o primeiro projeto de teste do repositório". Isso deixou de ser verdade
> entre a formalização do epic (2026-09-12) e sua implementação: `test-foundation-and-persistence-error-fixes.md`
> (concluído em `docs/epics/finalizados/`) já criou `Limaj.Framework.Functions.Tests` cobrindo
> exatamente os dois pontos abaixo — as 7 combinações de `ErrorType` em `ResultExtensionsTests.cs`
> e os 4 ramos do bridge de exceção em `ExceptionBridgeTests.cs`, já passando contra o código
> pré-rename. A Fase 1 original (criar o projeto + testes de caracterização) foi, portanto,
> absorvida: em vez de recriá-la, o projeto de teste existente foi renomeado junto com o pacote
> na Fase 2 (`Limaj.Framework.Functions.Tests` → `Limaj.Framework.Web.Tests`), preservando os
> testes de caracterização já existentes como a rede de segurança do rename. Decisão tomada com
> o usuário nesta sessão antes de prosseguir.

1. ~~Criar `Limaj.Framework.Web.Tests`~~ — já existia como `Limaj.Framework.Functions.Tests`
   (ver nota acima), cobrindo:
   - `ResultExtensions.MapError` — as 7 combinações de `ErrorType` → status code/corpo.
   - `ExceptionExtensions.ToHttpResult` — os 4 ramos (`DomainValidationException`,
     `NotFoundException`, `ConflictException`, default), incluindo o que é logado.
2. Testes confirmados passando **antes** do rename físico (herdados do epic de test-foundation).
3. Rename físico aplicado (`git mv` a nível de arquivo — ver nota de execução na Fase 2 —,
   namespace, `.csproj`) + ajuste de vocabulário (DA-002).
4. Os mesmos testes, renomeados junto com o projeto, passam **depois** do rename sem alteração
   de asserção — prova de rename puro, não reescrita disfarçada.
5. Teste específico adicionado para a correção de segurança (DA-004): erro não mapeado retorna
   mensagem genérica no corpo fora de ambiente de desenvolvimento; `ex.Message` original só
   aparece no log (`ExceptionBridgeTests.ToHttpResult_UnknownException_*`).

## Checklist de fases

### Fase 1 — Rede de segurança antes do rename
- [x] ~~Criar projeto `Limaj.Framework.Web.Tests`~~ — já existia (`Limaj.Framework.Functions.Tests`,
      criado por `test-foundation-and-persistence-error-fixes.md`); renomeado na Fase 2 em vez de
      recriado. Ver nota de execução acima.
- [x] Testes de caracterização de `ResultExtensions.MapError` (7 `ErrorType`) — já existentes
- [x] Testes de caracterização de `ExceptionExtensions.ToHttpResult` (4 ramos) — já existentes
- [x] Confirmar que todos os testes passam contra o código atual (`Limaj.Framework.Functions`) —
      confirmado via `dotnet test` antes do rename físico

### Fase 2 — Rename físico
- [x] Pacote renomeado para `packages/Limaj.Framework.Web` preservando histórico (rename a nível
      de arquivo via `git mv`/`git add`, não de diretório — um lock de processo em background do
      VS Code/OmniSharp impediu `git mv`/`Rename-Item` no diretório inteiro; git detectou o rename
      de qualquer forma pela similaridade de conteúdo de cada arquivo)
- [x] `.csproj` (nome do projeto/assembly), namespaces e referências em `Limaj.Framework.sln`
      atualizados; `Limaj.Framework.Architecture.Tests` também ajustado (referenciava o pacote)
- [x] Testes de caracterização da Fase 1 continuam passando sem alteração de asserção —
      confirmado via `dotnet test` (28/28 em `Limaj.Framework.Web.Tests`, 4/4 em
      `Limaj.Framework.Architecture.Tests`)

### Fase 3 — Ajuste de vocabulário e correção de segurança
- [x] `FunctionRunner` → `RequestRunner`; parâmetro `functionName` → `operationName` (DA-002)
- [x] Vazamento de `ex.Message` no ramo 500 corrigido (DA-004): mensagem genérica ao cliente fora
      de `ASPNETCORE_ENVIRONMENT=Development`, `ex.Message` completo permanece só no log +
      testes cobrindo os dois ramos (`ExceptionExtensions.cs`, `ExceptionBridgeTests.cs`)
- [x] Mensagens/nomes de log que referenciavam "function" atualizados para vocabulário neutro de
      host (`{Operation}`)

### Fase 4 — Atualização de documentação e consumidores
- [x] Tabela de camadas e seção "Exception bridge" em `CLAUDE.md` atualizadas
- [x] `README.md` (estrutura do repositório, fronteiras arquiteturais, building blocks) atualizado
- [x] Levantamento de consumidores: nenhum produto real referencia `Limaj.Framework.Functions` via
      `ProjectReference` ou NuGet publicado hoje (confirmado por busca textual no repo antes do
      rename — mesma constatação já registrada em DA-005 abaixo) — **sem necessidade de shim**,
      condição já prevista em DA-005

## Decisões pendentes

Nenhuma. DA-005 foi fechada abaixo — a informação que faltava (como o pacote é distribuído
hoje) foi levantada.

## Decisões arquiteturais (atualização — sessão `/flow` de 2026-09-12)

- **DA-005 — Sem shim de compatibilidade, condicionado à ordem de execução.** Levantamento
  direto (sem `PackageId`/`GeneratePackageOnBuild` em nenhum `.csproj`, sem `nuget.config`, sem
  workflow de publish no root do repo) confirmou que **não existe hoje nenhum mecanismo de
  distribuição** de `Limaj.Framework.Functions` — nem NuGet publicado, nem `ProjectReference`
  formal por nenhum produto real (`FinanceFlow` reimplementou tudo em paralelo). Não há
  consumidor a proteger, então **não há necessidade de shim**. Isso fica condicionado a uma
  ordem específica, fechada em conjunto com `/arquiteto` e `/analyst` na sessão que formalizou
  `docs/epics/backlog/nuget-package-publishing-pipeline.md`: **este epic (rename) precisa ser
  concluído antes do primeiro publish real de pacote.** Se essa ordem for respeitada,
  `Limaj.Framework.Functions` nunca chega a existir como pacote publicado — não há nada para o
  shim proteger. Se a ordem inverter (publish sair antes do rename), a resposta muda: passa a
  exigir shim de um ciclo de depreciação, porque aí sim se quebraria um contrato já publicado.

## Relacionado

- Investigação técnica que embasou este epic: comparação `Limaj.Framework.*` vs.
  `FinanceFlow` (sessão `/flow`, 2026-09-12), que também identificou outros gaps
  (extensibilidade de `ErrorType`/exceções, persistência, identidade, base de testes) — agora
  formalizados em `docs/epics/backlog/test-foundation-and-persistence-error-fixes.md`.
- DA-003 (vertical slicing via `IEndpoint`/`MapEndpoints`) fica registrada aqui como
  decisão de não fazer agora, não como item de trabalho — não vira epic próprio até o
  gatilho descrito ser acionado.
- `docs/epics/backlog/nuget-package-publishing-pipeline.md` — epic de publicação de pacotes,
  cuja Fase 2 depende deste epic (rename) estar concluído antes do primeiro publish real.
