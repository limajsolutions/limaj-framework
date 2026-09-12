# Renomear Limaj.Framework.Functions para Limaj.Framework.Web

**Status:** Backlog
**Última revisão:** 2026-09-12

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

Não existe hoje nenhum projeto de teste em `Limaj.Framework.sln`. Este epic cria o
**primeiro** projeto de teste do repositório (`Limaj.Framework.Web.Tests`, xUnit), e o
rename só é aceito como seguro se seguir esta ordem:

1. Criar `Limaj.Framework.Web.Tests` cobrindo, como testes de caracterização do código
   **atual** (antes de qualquer rename físico):
   - `ResultExtensions.MapError` — as 7 combinações de `ErrorType` → status code/corpo.
   - `ExceptionExtensions.ToHttpResult` — os 4 ramos (`DomainValidationException`,
     `NotFoundException`, `ConflictException`, default), incluindo o que é logado.
2. Rodar os testes **antes** do rename físico — devem passar contra o código atual.
3. Aplicar o rename físico (`git mv`, namespace, `.csproj`) + ajuste de vocabulário (DA-002).
4. Rodar os mesmos testes **depois** do rename — devem passar inalterados, provando que
   foi rename puro, não reescrita disfarçada.
5. Adicionar teste específico para a correção de segurança (DA-004): erro não mapeado em
   produção retorna mensagem genérica no corpo, `ex.Message` original só aparece no log.

## Checklist de fases

### Fase 1 — Rede de segurança antes do rename
- [ ] Criar projeto `Limaj.Framework.Web.Tests` (xUnit) e adicioná-lo a `Limaj.Framework.sln`
- [ ] Testes de caracterização de `ResultExtensions.MapError` (7 `ErrorType`)
- [ ] Testes de caracterização de `ExceptionExtensions.ToHttpResult` (4 ramos)
- [ ] Confirmar que todos os testes passam contra o código atual (`Limaj.Framework.Functions`)

### Fase 2 — Rename físico
- [ ] `git mv packages/Limaj.Framework.Functions packages/Limaj.Framework.Web` (preservando histórico)
- [ ] Atualizar `.csproj` (nome do projeto/assembly), namespaces e referências em `Limaj.Framework.sln`
- [ ] Confirmar que os testes de caracterização da Fase 1 continuam passando sem alteração

### Fase 3 — Ajuste de vocabulário e correção de segurança
- [ ] `FunctionRunner` → `RequestRunner`; parâmetro `functionName` → `operationName` (DA-002)
- [ ] Corrigir vazamento de `ex.Message` no ramo 500 (DA-004) + teste cobrindo o novo comportamento
- [ ] Atualizar mensagens/nomes de log que referenciam "function" para vocabulário neutro de host

### Fase 4 — Atualização de documentação e consumidores
- [ ] Atualizar tabela de camadas e seção "Exception bridge" em `CLAUDE.md`
- [ ] Atualizar `README.md` (estrutura do repositório)
- [ ] Levantar se `Limaj.Framework.Functions` é consumido hoje via `ProjectReference` ou
      NuGet publicado, e decidir se é necessário shim de compatibilidade temporário antes
      de comunicar a mudança a quem consome o pacote

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
