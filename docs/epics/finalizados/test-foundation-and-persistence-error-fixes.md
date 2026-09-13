# Fundação de testes, correção do UnitOfWork e extensibilidade de persistência/erro

**Status:** Finalizado (implementação — sign-off funcional é decisão do usuário)
**Última revisão:** 2026-09-12

## Contexto

Uma investigação técnica (`/spike`, modo investigação) comparando `packages/Limaj.Framework.*`
com um produto real construído de forma independente (`FinanceFlow`) revelou que o framework
não tem nenhuma rede de testes hoje (`Limaj.Framework.sln` não contém nenhum projeto de teste)
e carrega um bug conhecido de infraestrutura de persistência, além de dois pontos de
extensibilidade aditiva que o FinanceFlow precisou reimplementar em paralelo por falta deles
no framework.

`/arquiteto` e `/analyst`, consultados via `/flow`, convergiram sem necessidade de
contraposição sobre os quatro itens abaixo. Depois, numa sessão seguinte sobre a criação de
uma pipeline de publicação de pacotes (ver Relacionado), os dois especialistas convergiram de
novo em que **a ausência de testes é bloqueante para publicar qualquer pacote versionado** —
publicar sem teste é distribuir como "verificado" o que só foi compilado. Isso torna a Fase 2
deste epic (fundação de testes) um pré-requisito direto do epic de publicação, não apenas um
item de qualidade desejável isolado.

## Decisões arquiteturais

- **DA-001 — Bug do `UnitOfWork<TDbContext>` (P0, correção pura).**
  `ExecuteInTransactionAsync` é incompatível com `EnableRetryOnFailure`: o EF Core exige que
  qualquer código que rode dentro de uma execution strategy com retry seja executado via
  `Database.CreateExecutionStrategy().ExecuteAsync(...)`, e o método atual não faz isso — abre
  transação diretamente, o que falha (ou mascara falha de retry) quando o `DbContext` está
  configurado com retry habilitado. Não há decisão de design pendente aqui, é correção de bug.

- **DA-002 — Utilitários de persistência aditivos (P1).** Três extension methods novos, sem
  alterar nenhum contrato existente: `DesignTimeDbContextFactoryBase` (base para
  `IDesignTimeDbContextFactory<T>` usada por migrations), `UseSqlServerWithRetry` (wrapper de
  `UseSqlServer` já configurando `EnableRetryOnFailure` com os parâmetros corretos, evitando
  reincidência do bug DA-001 em quem configura retry manualmente) e
  `UseUtcDateTimeConversion` (value converter para forçar `DateTime`/`DateTimeOffset` como UTC
  no mapeamento EF Core).

- **DA-003 — Mecanismo de extensibilidade de erro (P1).** `Error` ganha um `HttpStatusCode`
  opcional (nullable), e passa a existir um contrato `IExceptionToErrorMapper` plugável que o
  host (`Limaj.Framework.Functions`/`Web`) pode resolver via DI para mapear exceções não
  cobertas pela ponte padrão. **`ErrorType` continua fechado em 7 valores — não ganha novos
  membros.** Restrição crítica, sem excepção: nenhum tipo de erro concreto de domínio de
  produto (ex. `PlanLimitExceeded`) pode subir para dentro do framework — apenas o mecanismo de
  extensão. Um teste deve travar isso (ver Fase 4).

- **DA-004 — Fundação de testes (pré-requisito bloqueante para publicação de pacotes).** Um
  projeto de teste por pacote (`Limaj.Framework.Abstractions.Tests`,
  `Limaj.Framework.Application.Tests`, `Limaj.Framework.Persistence.EFCore.Tests`,
  `Limaj.Framework.Functions.Tests`), adicionado a `Limaj.Framework.sln`, cobrindo os contratos
  centrais que o `CLAUDE.md` já nomeia como building blocks reutilizáveis — são exatamente as
  peças que um consumidor externo (futuro) vai depender de não quebrar silenciosamente.

- **DA-005 — Teste de arquitetura da tabela de dependência.** Um teste baseado em reflection
  (ex. NetArchTest) que assevera programaticamente a direção de dependência entre os 4 pacotes
  (`Abstractions` não referencia nada; `Application`/`Persistence.EFCore`/`Functions`
  referenciam só `Abstractions` + suas dependências externas declaradas no `CLAUDE.md`). O
  build já pega isso por referência de projeto, mas o teste resiste a um contorno futuro via
  reflection/tipos dinâmicos que o build não pegaria.

## Estrutura proposta por camada

```
packages/
  Limaj.Framework.Abstractions/
    Limaj.Framework.Abstractions.Tests/        (novo)
  Limaj.Framework.Application/
    Limaj.Framework.Application.Tests/          (novo)
  Limaj.Framework.Persistence.EFCore/
    src/Persistence/Extensions/
      DesignTimeDbContextFactoryBase.cs          (novo)
      SqlServerRetryExtensions.cs                (novo — UseSqlServerWithRetry)
      UtcDateTimeConversionExtensions.cs          (novo — UseUtcDateTimeConversion)
    Limaj.Framework.Persistence.EFCore.Tests/    (novo)
  Limaj.Framework.Functions/
    Limaj.Framework.Functions.Tests/             (novo)
  Limaj.Framework.Architecture.Tests/             (novo — teste de dependência entre pacotes, DA-005)
```

`Limaj.Framework.sln` passa a incluir 5 novos projetos de teste, mantendo a mesma direção de
dependência já documentada no `CLAUDE.md` (nenhum projeto de teste de uma camada referencia
implementação de outra além do que a própria camada já referenciaria em produção).

## Estratégia de testes

- **`Limaj.Framework.Abstractions.Tests`** — `Result`/`Result<T>` (todas as combinações de
  sucesso/erro), `Error` (incluindo o novo `HttpStatusCode` opcional de DA-003).
- **`Limaj.Framework.Application.Tests`** — nada de domínio a testar hoje (pacote é
  intencionalmente vazio de regra de negócio); cobre apenas contratos/abstrações que existirem
  na camada, se houver.
- **`Limaj.Framework.Persistence.EFCore.Tests`** — `BaseRepository` (soft delete via
  `IsActive`, opt-in `includeInactive`, `HardDeleteByIdAsync`), `UnitOfWork<TDbContext>`
  (commit/rollback, e o cenário específico do bug DA-001: transação com retry habilitado não
  falha), os 3 utilitários novos de DA-002.
- **`Limaj.Framework.Functions.Tests`** — `ResultExtensions.ToHttpResult` (as 7 combinações de
  `ErrorType` → status/corpo), `ExceptionExtensions`/`FunctionRunner.RunAsync` (os 4 ramos:
  `DomainValidationException`, `NotFoundException`, `ConflictException`, default), e o
  `IExceptionToErrorMapper` plugável de DA-003 (incluindo o teste negativo: nenhum tipo de erro
  de domínio de produto é referenciado no assembly do framework).
- **`Limaj.Framework.Architecture.Tests`** — só o teste de dependência de DA-005.

Sem gate de percentual de cobertura (`coverlet`/threshold) — cobertura comportamental dos
contratos públicos é o critério, não métrica arbitrária.

## Checklist de fases

### Fase 1 — Correção de bug (P0)
- [x] Corrigir `UnitOfWork<TDbContext>.ExecuteInTransactionAsync` para usar
      `Database.CreateExecutionStrategy().ExecuteAsync(...)`
- [x] Teste de caracterização cobrindo transação com `EnableRetryOnFailure` habilitado

### Fase 2 — Fundação de testes (bloqueante para o epic de publicação, ver Relacionado)
- [x] Criar os 4 projetos de teste (um por pacote) + `Limaj.Framework.Architecture.Tests`,
      adicionar todos a `Limaj.Framework.sln`
- [x] Cobertura de `Result`/`Error`/`ResultExtensions.ToHttpResult` (7 `ErrorType`)
- [x] Cobertura da ponte de exceção (`FunctionRunner.RunAsync` + `ExceptionExtensions`, 4 ramos)
- [x] Cobertura de `BaseRepository` (soft delete, `includeInactive`, `HardDeleteByIdAsync`) —
      **achado fora do escopo original, corrigido nesta sessão com aval explícito do usuário**:
      `BaseEntityConfiguration<T>.Configure()` nunca chamava `HasQueryFilter(e => e.IsActive)`,
      então soft delete não excluía nada por padrão e `includeInactive` (`IgnoreQueryFilters()`)
      era inerte (não havia filtro para ignorar). Corrigido adicionando o `HasQueryFilter`. Como
      consequência direta dessa correção, `SoftDeleteByIdAsync`/`RestoreByIdAsync` (que
      consultavam via `Set.Where(...)`, agora sujeito ao filtro) e `HardDeleteByIdAsync`
      (delegava para o agora-filtrado `GetByIdAsync`, e usava `AsNoTracking()` causando conflito
      de identidade quando a entidade já estava rastreada no mesmo `DbContext`) também precisaram
      de ajuste — todos os três agora usam `IgnoreQueryFilters()`, e `HardDeleteByIdAsync` deixou
      de usar `AsNoTracking()` para permitir resolução de identidade do EF Core. Coberto por 9
      testes novos em `BaseRepositoryTests`.
- [x] Cobertura de `UnitOfWork` (commit/rollback) — coberta pelos 3 testes de caracterização da
      Fase 1 (`UnitOfWorkExecuteInTransactionAsyncTests`)
- [x] Teste de arquitetura validando a tabela de dependência entre os 4 pacotes

### Fase 3 — Utilitários de persistência (P1)
- [x] `DesignTimeDbContextFactoryBase`
- [x] `UseSqlServerWithRetry`
- [x] `UseUtcDateTimeConversion`
- [x] Testes cobrindo os 3 utilitários

### Fase 4 — Mecanismo de extensibilidade de erro (P1)
- [x] `Error.HttpStatusCode` opcional
- [x] `IExceptionToErrorMapper` plugável, resolvido via DI no host
- [x] Teste garantindo que `ErrorType` permanece fechado em 7 valores
- [x] Teste negativo garantindo que nenhum tipo de erro concreto de domínio de produto é
      referenciado dentro do framework

## Decisões pendentes

> Decisão pendente: DA-006 — se o pacote `Limaj.Framework.Testing` (+ `Testing.Functions`),
> levantado na mesma investigação original como P1 (`TestBase` AutoFixture+AutoMoq, specimen
> builders, fake `IUserIdentityGateway`, host de integração SQLite in-memory), entra neste epic
> ou vira epic próprio. Por decisão do usuário nesta sessão, **não** foi incluído aqui — seu
> escopo é diferente (criação de um pacote novo de utilitários de teste *para consumidores*,
> não a rede de testes do próprio framework) e ainda não foi formalizado.

## Relacionado

- **Bloqueia** `docs/epics/backlog/nuget-package-publishing-pipeline.md` — a Fase 2 deste epic
  (fundação de testes) precisa estar concluída antes do primeiro publish real de pacote.
- Investigação técnica que originou os gaps: comparação `Limaj.Framework.*` vs. `FinanceFlow`
  (sessão `/flow`, 2026-09-12).
- Não relacionado ao epic `rename-functions-to-web.md` além da ordem de sequenciamento definida
  no epic de publicação (o rename precede o primeiro publish, este epic de testes também
  precede — os três epics compartilham o mesmo marco "antes do primeiro publish real").
