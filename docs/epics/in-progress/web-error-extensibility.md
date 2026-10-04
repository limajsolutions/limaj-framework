# Web error extensibility — extension point, Problem Details format and error contract (3.0.0 → 4.0.0)

**Status:** In progress — not business-approved (that is the user's decision)
**Última revisão:** 2026-10-04

## Context

A real consumer, a new ASP.NET Core .NET 10 project built on Minimal APIs and consumed by a
SPA and by AI agents over MCP, sent a formal request. Its analysis concluded that
`Limaj.Framework.Web` does not meet its HTTP error contract and that `Error` in
`Limaj.Framework.Abstractions` needs adjusting. Without these changes, the consumer would have
to build its own Result/Error core, which defeats the goal of standardizing products on this
framework.

The consumer's pains, all confirmed against the code:

1. `title = Error.Message`, `detail = Error.Code`, so the client has no stable field to branch
   on, and `ValidationProblem` carries no code at all.
2. 429 goes out without `Retry-After`, and there is no way to supply the value.
3. 422 is only reachable through the `Error.HttpStatusCode` escape hatch.
4. 404/409 are built with `Results.NotFound/Conflict(ProblemDetails)` and bypass the
   `IProblemDetailsService`.
5. `ResultExtensions` is a static class with a private `MapError`. `IExceptionToErrorMapper`
   chooses *which* `Error` comes out, not *how* the response is written.
6. `ExceptionExtensions` decides whether to expose `ex.Message` by reading
   `ASPNETCORE_ENVIRONMENT`.
7. `Error.HttpStatusCode` is a transport concept inside a domain error, and it is useless to
   other transports such as an MCP tool.
8. `ErrorType` has no value for a business rule violated with valid input.

Facts verified in the `/flow` session (an experiment executing the framework's `IResult`s on a
`DefaultHttpContext` on .NET 10):

- With `AddProblemDetails` registered, everything that comes out as `ProblemHttpResult`
  (Validation, Forbidden, Unauthorized, TooManyRequests, Unexpected, custom `HttpStatusCode`)
  already goes through `IProblemDetailsService`. `CustomizeProblemDetails` is applied and
  `traceId` is added automatically.
- NotFound/Conflict come out as `NotFound<ProblemDetails>`/`Conflict<ProblemDetails>`. They
  bypass the pipeline: no customization, no `traceId`, and `application/json` instead of
  `application/problem+json`.
- A custom `IProblemDetailsWriter` registered **before** `AddProblemDetails` replaces the whole
  format (tested with a `{success, error}` envelope). Registered **after**, the default writer
  wins.
- `CustomizeProblemDetails` receives `HttpContext` + `ProblemDetails`, never the original
  `Error`.
- `Error.Details` only reaches the body for `Validation`. For every other type it is dropped,
  and it is dropped even for `Validation` when `HttpStatusCode` is set.
- The 500 path for an unmapped exception puts `ex.Source` (an assembly name such as
  `Microsoft.EntityFrameworkCore`) into `detail`, even outside Development.
- A `Result` with `ErrorType.Unexpected` sends `Error.Message` to the client with none of
  DA-004's protection (`rename-functions-to-web`).
- An error mapped by the host's `IExceptionToErrorMapper` is always logged at Warning, even
  when it results in a 5xx.

The decisions below were settled in a `/flow` mediation session between `/arquiteto` and
`/analyst` (2 rounds, each with a counter-argument). The user decided the points where the
specialists diverged, and the points that were the user's to make.

**Not requested by the consumer, so it stays in the consumer:** error mapping for MCP tools,
the error-code catalog and its OpenAPI publication, and success responses (e.g. 202 for an
action pending approval). The framework only has to make these possible through DA-002.

## Architectural decisions

> **Version labels updated by DA-013** (in the `result-core-package-split` commit): in
> DA-002…DA-011 and the sections below them, what was "2.1.0"/"2.x" (first shipping) now
> reads 3.0.0/3.x, and what was "3.0.0" (removals, default flips) now reads 4.0.0. DA-001's
> policy and its example labels are unchanged; DA-012 keeps its original labels as history.

- **DA-001 — Version policy.** *(user)*
  - **Minor (2.x):** an additive change, or an opt-in that keeps the current behavior as the
    default. Adding `[Obsolete]` is also minor.
  - **Major (3.0.0):** a change to the default behavior, a removal, or a signature change.
    These ship with a migration note in `CHANGELOG.md`.
  - **New compiler warnings that break consumers on `TreatWarningsAsErrors`:** a new `ErrorType`
    member (CS8509 on exhaustive `switch` expressions) counts as **major**.
  - **Deprecation path:** when possible, `[Obsolete]` lands in a minor and the removal in the
    next major.

- **DA-002 — W1: `IErrorHttpMapper` extension point, with the static facades kept.**
  *(consensus)* ADR candidate.
  - **Contract:** a new `IErrorHttpMapper { IResult Map(Error error) }` lives in
    **`Limaj.Framework.Web`**. It cannot live in `Abstractions`, because it returns `IResult`
    and the architecture test forbids ASP.NET references there.
  - **Default mapping:** `DefaultErrorHttpMapper` is public, with one public method per
    `ErrorType`, so a product decorates or inherits it instead of rewriting it.
  - **Entry point:** a new injected service (working name `IHttpResultResponder`) exposes
    `ToHttpResult(result, onSuccess)` and `RunAsync(handler, operationName)`. It combines
    `IErrorHttpMapper`, `IExceptionToErrorMapper`, the logger and
    `IOptions<LimajHttpErrorOptions>`, and is registered with `AddLimajHttpErrors(...)`.
  - **Registration constraint:** `AddLimajHttpErrors` must **not** register an
    `IProblemDetailsWriter`, because of the registration-order fact above.
  - **One path for exceptions and results:** exception → `Error` (built-ins, then
    `IExceptionToErrorMapper`, then Unexpected) → `IErrorHttpMapper`. A single place decides
    how every error response is written.
  - **Static facades:** `ResultExtensions.ToHttpResult` and `RequestRunner.RunAsync` keep their
    signature, concrete return types and body in 3.x. They delegate to the default mapping in
    `V2` format (DA-003).
  - **Rejected — a "deferred" `IResult` resolving the mapper from `RequestServices` at
    execution time:** it changes the concrete type returned, which breaks consumer tests that
    cast to `ProblemHttpResult`. It also hides service location.
  - **Rejected — a per-call `onFailure` overload:** it spreads the error contract across
    routes.
  - **Version:** minor-sized under DA-001; ships in 3.0.0 (DA-013).

- **DA-003 — W2 + W3 + field semantics: one Problem Details format selector.** *(user, following
  the architect)*
  - **The selector:** `LimajProblemDetailsFormat { V2, V3 }` in `LimajHttpErrorOptions`.
  - **`V2`:** the current output, unchanged.
  - **`V3` is the one named contract that bundles:**
    - `extensions.code = Error.Code` on every error response, including `ValidationProblem`
      (W2);
    - 404/409 via `Results.Problem`, so every error goes through `IProblemDetailsService`
      (W3);
    - `detail = Error.Message`, with `title` by status.
  - **Defaults:** `V2` in 3.0.0. `V3` becomes the default in 4.0.0, and `V2` stays as the
    migration value. The major only flips the default; the API shape does not change.
  - **Why a single selector:** the three items change the same response body. Independent
    flags would create 2³ hybrid contracts that never exist as a default, and multiply the
    tests.
  - **Divergence recorded:** the analyst preferred independent W2/W3 flags in 2.x with no early
    `detail = Message`, so a consumer could take `code` alone without the field-semantics
    change. The user chose the architect's selector.
  - **Documented caveat:** with `V2`, `CustomizeProblemDetails` is not global. It does not
    reach 404/409 and gets no `code`. It covers every error response only with `V3`.
  - ADR candidate: flipping the default format in 4.0.0 while keeping `V2` as the migration
    value.

- **DA-004 — W4: exposure of exception details by explicit option.** *(user, following the
  architect)*
  - **3.0.0:** `bool? IncludeExceptionDetails` in `LimajHttpErrorOptions`.
    - `null` keeps today's environment read (no behavior change). The default options must
      reproduce it, so the static facade and the mapper share one code path.
    - `true` or `false` always wins.
    - `true` logs a fixed, data-free `Warning` at startup without consulting the environment,
      and the app still starts.
  - **Even with `true`:** only `ex.Message` is exposed. Never the stack trace, inner exceptions
    or `Exception.Data`. A test locks this in.
  - **4.0.0:** the type becomes `bool`, default `false`, and the framework stops reading any
    environment name.
  - **Divergence recorded:** the analyst preferred default `false` on the new DI path and a
    startup failure when `true` outside Development, on LGPD grounds. The user chose the
    architect's position. A hard lock would again make behavior depend on the environment
    name, which the consumer asked to eliminate.
  - **Documentation for 3.x:**
    - On the Azure Functions isolated worker, `ASPNETCORE_ENVIRONMENT` is usually absent (the
      host uses `AZURE_FUNCTIONS_ENVIRONMENT`), so Functions consumers should set
      `IncludeExceptionDetails` explicitly.
    - Do not add a read of `AZURE_FUNCTIONS_ENVIRONMENT`; 4.0.0 removes environment reads.

- **DA-005 — Security fixes on by default in 3.0.0, as an explicit exception to DA-001.**
  *(user, following both specialists' recommendation)*
  - **The fixes:**
    - **`ex.Source` leaves the 500 body.** The code becomes a fixed `unexpected_error`. Sending
      assembly names to the client is fingerprinting (CWE-209), and no legitimate client
      depends on it. `ex.Source` stays in the log.
    - **DA-004 (`rename-functions-to-web`) extends to `Result` with `ErrorType.Unexpected`.**
      Outside Development the client gets the generic message. This protects against
      `Result.Unexpected(code, ex.Message)` leaking SQL, configuration or personal data. A
      temporary opt-out (`ExposeUnexpectedResultMessage`) ships marked `[Obsolete]` and is
      removed in 4.0.0. A product that wants a user-facing message should use another
      `ErrorType`.
    - **`Validation` with `HttpStatusCode` set stops dropping `errors`.** This is a bug:
      validation details are public by design.
  - **Release notes:** a "Security" section in `CHANGELOG.md`.

- **DA-006 — Exception bridge and logging.** *(consensus)*
  - **Precedence:** the 3 built-in exceptions keep priority over the host's mapper by default.
    Inverting the order would silently turn 404/409/400 into whatever a host catch-all
    returns.
  - **Public built-in mapping:** it is exposed as a public `IExceptionToErrorMapper`, so a
    host can compose its own order explicitly.
  - **No composite chain class for now:** with the built-in mapping public, a host composes a
    chain in a few lines.
  - **Log level by resulting status:** an error mapped by the host is logged by its resulting
    status (5xx → `Error` with the exception; 4xx → `Warning`) instead of a fixed `Warning`.
    The classification comes from the `Error` (Unexpected, or a status ≥ 500), not from the
    `IResult`, so it stays testable without an `HttpContext`.
  - **Stable `EventId`s:** generated with `LoggerMessage`, no new logging knobs. 4xx stays
    strictly below 5xx.
  - **Version:** minor, with a `CHANGELOG.md` note, since new alerts may fire.

- **DA-007 — W5: typed `RetryAfter` on `Error`.** *(consensus)*
  - **The field:** `TimeSpan? RetryAfter` as an `init` property **outside** the positional
    constructor, so the constructor signature does not change. It is transport-neutral, so an
    MCP agent also knows how long to wait.
  - **Header:** the default mapping emits `Retry-After` in whole seconds, rounded up, only when
    the value is set and > 0.
  - **Body:** the field goes in the body only in `V3`.
  - **ASP.NET rate limiter:** its 429 rejects before the handler and never goes through the
    framework. The host handles it in `RateLimiterOptions.OnRejected` (`MetadataName.RetryAfter`),
    and the framework only documents the recipe.
  - **Generic metadata:** this is a narrow, typed exception to "no generic `Metadata` bag on
    `Error`", which still stands. A generic bag invites personal data and an untyped contract.
  - **Version:** minor.

- **DA-008 — `Error.Details` outside Validation stays opt-in, in 3.x and in 4.x.** *(consensus)*
  - **Why opt-in:** products may have put internal or personal data in the `Details` of
    NotFound/Conflict/etc. counting on it being dropped. Exposing it by default would be a
    silent leak (privacy by default, LGPD art. 46 / GDPR art. 25).
  - **Never on 5xx:** `Unexpected`/5xx never send `Details`, even with the opt-in.
  - **Version:** minor.

- **DA-009 — A1: `ErrorType.BusinessRule = 8` → 422, in 4.0.0.** *(acceptance: consensus;
  version: user, per DA-001)* ADR candidate.
  - **Meaning:** a generic category, not a concrete product type. "Valid input, a rule forbids
    the operation" sits between Validation ("fix the input") and Conflict ("concurrent or
    duplicate state"). The boundary is defined in [GLOSSARY.md](../../../GLOSSARY.md) under
    *Business rule violation*. MCP agents in particular gain the semantic distinction.
  - **Supersedes in part DA-003 of `done/test-foundation-and-persistence-error-fixes.md`:**
    the letter ("`ErrorType` closed at 7") changes; the spirit ("no concrete product error type
    in the framework") stands.
    - The closed-set test
      `ErrorTests.ErrorType_RemainsClosedToTheSevenDocumentedValues` changes to 8 values.
      This is an intentional rule change, recorded in the commit.
  - **Numbering:** the existing values are not renumbered.
  - **Scope:** a `Result.BusinessRule(...)` factory and the 422 mapping. No built-in exception
    maps to it in this epic.
  - **Version:** major, because CS8509 breaks consumers with `TreatWarningsAsErrors`.
  - **In 3.x:** a consumer gets 422 through `IErrorHttpMapper` keyed on `Error.Code`
    (recommended), or through the `HttpStatusCode` escape hatch.

- **DA-010 — A2: deprecate `Error.HttpStatusCode`.** *(acceptance: consensus; version: user,
  per DA-001)* ADR candidate.
  - **Schedule:** `[Obsolete]` in 3.0.0, removal in 4.0.0.
  - **Why it goes:** a transport concept does not belong in the domain error.
  - **Replacement:** statuses outside the `ErrorType` mapping (402, 410, 412, 503, and 422
    until DA-009 ships) are resolved by the product's `IErrorHttpMapper` keyed on
    `Error.Code`. That mapper exists in the same minor (DA-002), so the deprecation never
    leaves a consumer without a supported path.
  - **Mechanics for `/spike` to validate:** `HttpStatusCode` is a positional record parameter.
    Making the warning fire on construction without a binary break probably needs an explicit
    constructor plus an obsolete overload. The framework itself will need `#pragma` to keep
    reading the property in 3.x.
  - **Note (DA-013, `result-core-package-split`):** "without a binary break" was written for a
    2.1.0 minor. With the 3.0.0 release, `Error` moved to `Limaj.Framework.Core`, so only
    **source** compatibility holds (the 2.0 constructor and `Deconstruct` are kept); consumers
    recompile.

- **DA-011 — Out of scope.** *(consensus)*
  - **Consumer-owned:** error mapping for MCP tools, the code catalog and its OpenAPI
    publication, and success responses.
  - **A3, splitting the result core into its own package:** tracked in
    `done/result-core-package-split.md`.
  - **A4, typed user principal:** tracked in `done/typed-user-principal.md`.

- **DA-012 — Release sequencing: 2.1.0 ships before 3.0.0.** *(consensus — `/flow` run of
  2026-10-04)* — **Superseded by DA-013** (kept as history).
  - **Scope of the run:** Phases 1–7 (2.1.0) only. Phase 8 (3.0.0) is a later run, after
    2.1.0 is merged and tagged.
  - **Why:** DA-001's deprecation path (`[Obsolete]` in a minor, removal in the next major)
    only holds if 2.1.0 actually reaches consumers. One pull request carrying both would add
    and remove `HttpStatusCode` and `ExposeUnexpectedResultMessage` in the same merge, and a
    2.1.0 tag could only go on an intermediate commit (lost on a squash merge). 3.0.0 is also
    the natural window for `result-core-package-split`, which still needs mediation.
  - **Tags are the user's:** an implementation run never tags. "Cut the 2.1.0 tag" stays
    `- [ ]` until the user cuts it after the merge, so this epic stays in `in-progress/`.
  - **`CHANGELOG.md`:** the 2.1.0 entry does not claim a release date before the tag exists.

- **DA-013 — PR #1 ships as 3.0.0; 2.1.0 is never released; removals move to 4.0.0.**
  *(user, for the version — consensus, for the consequences — `/flow` run of 2026-10-04)*
  Supersedes DA-012.
  - **Version (user):** the pull request that carries Phases 1–7 also carries
    `result-core-package-split` and `typed-user-principal`, and ships as **3.0.0**. The major
    is what warns current consumers. The run's scope is those two epics plus Phases 1–7 of this
    one; Phase 8 is not in it.
  - **Why the removals cannot go into 3.0.0 anyway (consensus):** with no 2.1.0, 3.0.0 is the
    first release in which consumers see the `[Obsolete]` items. Removing them in the same
    release would break DA-001's deprecation path. So 3.0.0 is the deprecation release and the
    next major, **planned as 4.0.0**, is the removal release.
  - **Consequences (consensus), applied in the `result-core-package-split` commit:**
    - every "removed in 3.0.0" / "until 3.0.0" / "3.0.0 makes V3 the default" text in code,
      XML docs, `[Obsolete]` messages, the Web README, test comments and this epic → **4.0.0**;
    - where "2.1.0" describes behavior shipping for the first time ("on by default in 2.1.0",
      "default in 2.x"), it becomes 3.0.0; purely historical notes stay as they are;
    - `CHANGELOG.md` `[Unreleased]` is reframed as "Planned as 3.0.0, a major release", with
      BREAKING entries for the split and the principal and an "Upgrading from 2.x to 3.0.0"
      section; still no version heading and no date until the user tags;
    - `Error.HttpStatusCode` stays `[Obsolete]` when `Error` moves to `Limaj.Framework.Core`,
      so consumers keep the migration message;
    - Phase 8 is renamed "4.0.0" and also removes `IUserIdentityGateway`'s obsolete members
      (`typed-user-principal` DA-006); "Cut the 2.1.0 tag" becomes "Cut the 3.0.0 tag".
  - **Accepted cost:** the V3 default, `BusinessRule` and the removals wait for a second major.
    Phase 8 could still join 3.0.0 before the tag if the user widens the scope; in that case the
    identity obsolete members are removed in 3.0.0 too (all removals in the same major).

## Proposed structure by layer

```
packages/
  Limaj.Framework.Core/src/                (moved from Abstractions/src/Common by result-core-package-split)
    Error.cs                          (RetryAfter init property — DA-007; HttpStatusCode [Obsolete] — DA-010;
                                       4.0.0: HttpStatusCode removed)
    Result.cs                         (4.0.0: BusinessRule factory — DA-009)
    ErrorType (in Error.cs)           (4.0.0: BusinessRule = 8 — DA-009)
  Limaj.Framework.Web/src/Http/
    IErrorHttpMapper.cs               (new — DA-002)
    DefaultErrorHttpMapper.cs         (new — public, one method per ErrorType, V2/V3 — DA-002/DA-003)
    BuiltInExceptionToErrorMapper.cs  (new — public built-in exception mapping — DA-006)
    IHttpResultResponder.cs           (new — injected entry point — DA-002; final name at implementation)
    LimajHttpErrorOptions.cs          (new — Format, IncludeExceptionDetails, Details opt-in,
                                       temporary Unexpected opt-out — DA-003/004/005/008)
    LimajProblemDetailsFormat.cs      (new — V2 | V3 — DA-003)
    HttpErrorsServiceCollectionExtensions.cs  (new — AddLimajHttpErrors — DA-002)
    ResultExtensions.cs               (static facade, unchanged contract; delegates to default mapping V2)
    ExceptionExtensions.cs            (static facade; security fixes DA-005; log by status DA-006)
    RequestRunner.cs                  (static facade, unchanged contract)
```

No new package, and the dependency direction is unchanged: `IErrorHttpMapper` and everything
that touches `IResult` stay in `Web`.

## Test strategy

- **Characterization first, before any refactor:** lock the current v2 behavior of the static
  facades: status, `Content-Type`, `title`/`detail`, and concrete result type for the 7
  `ErrorType`s, custom `HttpStatusCode`, and the 4 exception branches. Every change of this
  epic must leave these green, except the DA-005 fixes, which update their tests explicitly.
- **Contract suite (from the session's experiment):** execute each `IResult` on a
  `DefaultHttpContext` with a real service provider, with and without `AddProblemDetails`. One
  theory per `ErrorType` × format (`V2`/`V3`) asserts:
  - status and `Content-Type`;
  - body snapshot (`title`, `detail`, `extensions.code`, `errors`);
  - `traceId` present when `AddProblemDetails` is registered;
  - customization applied (every case under `V3`);
  - `Retry-After` header when `RetryAfter` is set.
- **Facade equivalence:** the static facade produces the same body as the mapper in `V2`.
- **`IncludeExceptionDetails`:** the `null`/`true`/`false` states, with the environment reader
  injected and no process-variable mutation. `true` never leaks the stack trace or inner
  exceptions. The startup `Warning` is asserted with a fake `ILogger`.
- **Security (DA-005):** outside Development, the 500 body carries neither `ex.Source` nor
  `ex.Message`, and a `Result.Unexpected` body carries the generic message.
- **Bridge (DA-006):**
  - a host catch-all mapper does not shadow the built-ins;
  - a mapped 5xx is logged at `Error` with the exception, a mapped 4xx at `Warning`.
- **`Details` (DA-008):** no `Details` outside Validation without the opt-in, and never on 5xx
  even with it.
- **Integration:** `AddLimajHttpErrors` + `AddProblemDetails` in a minimal host. Every output
  goes through `IProblemDetailsService` with `V3`, and a custom `IProblemDetailsWriter`
  registered before `AddProblemDetails` replaces the format.

## Implementation notes (3.0.0)

Choices made where the decisions above left the mechanics open:

- **Names:** the entry point is `IHttpResultResponder` (internal implementation
  `HttpResultResponder`, scoped). `DefaultErrorHttpMapper` has one virtual method per `ErrorType`
  plus a public `MapWithStatusCode(error, status)`. That method is the supported replacement for
  `HttpStatusCode` (DA-010), and it stays in 4.0.0.
- **DA-010 mechanics:** `Error` became an explicit (non-positional) record. It keeps the 2.0
  five-parameter constructor and five-value `Deconstruct` with identical signatures, both
  `[Obsolete]`, plus the obsolete property. A new four-parameter constructor carries
  `[OverloadResolutionPriority(1)]`, so `new Error(code, message)` binds to it without a
  warning or an ambiguity. `new Error(..., HttpStatusCode: x)` binds to the obsolete one and
  warns. `ErrorTests.HttpStatusCode_IsObsolete_AndV2SignaturesAreKeptForSourceCompatibility` locks this in.
- **DA-006 order override:** the responder runs `BuiltInExceptionToErrorMapper` (resolved from
  DI) before the host's `IExceptionToErrorMapper`. A host that wants another order registers a
  subclass of `BuiltInExceptionToErrorMapper` in its place (README recipe), so no composite
  class is needed.
- **DA-005 scope of the generic message:** it applies to `ErrorType.Unexpected` only when the
  resolved status is ≥ 500. An `Unexpected` error with an explicit 4xx `HttpStatusCode` (the
  2.0 README's own 402 example) keeps its message. "Outside Development" is the same resolution
  as `IncludeExceptionDetails` (`null` → `ASPNETCORE_ENVIRONMENT`).
- **Retry-After:** applied by wrapping the result in a public `RetryAfterHttpResult` (it exposes
  `InnerResult` and `StatusCode`), only when `RetryAfter > 0`. Every other error keeps its 2.0
  concrete type. Under `V3` the body field is `retryAfter`, in seconds.
- **DA-008 body field:** opted-in `Details` go out as the `details` extension, in both formats.
- **Startup warning:** an `IHostedService` registered by `AddLimajHttpErrors` (EventId 1000).
  The ids 4000–4003 are 4xx events and 5000–5001 are 5xx events.

## Phase checklist

### Phase 1 — Characterization (3.0.0)
- [x] Characterization tests for the static facades' current v2 behavior (status, Content-Type,
      title/detail, concrete type; 7 `ErrorType`s, custom `HttpStatusCode`, 4 exception branches)
- [ ] Verify on an Azure Functions isolated host (ASP.NET Core integration) that
      `HttpContext.RequestServices`, `AddProblemDetails`/`IProblemDetailsService` and
      `IOptions` validation behave as on Minimal API; record the result in this epic
  - **Result (2026-10-04 run): not verified.** This repository has no Azure Functions project,
    and the machine has no Azure Functions Core Tools (`func`) to run an isolated host. The
    item stays open until someone runs it in a Functions app. What to check there: an endpoint
    that injects `IHttpResultResponder`; `AddProblemDetails` adding `traceId` under `V3`; the
    `ExceptionDetailsStartupWarning` hosted service logging when `IncludeExceptionDetails = true`;
    and `AddLimajHttpErrors(o => o.Format = (LimajProblemDetailsFormat)42)` failing on the
    first `IOptions<LimajHttpErrorOptions>.Value`. On Minimal API these are covered by
    `AddLimajHttpErrorsTests` and `ExceptionDetailsOptionTests`, against a real service
    provider and `DefaultHttpContext`.

### Phase 2 — Extension point (3.0.0, DA-002, DA-006)
- [x] `IErrorHttpMapper` + public `DefaultErrorHttpMapper` (one public method per `ErrorType`)
- [x] Public built-in exception mapping (`IExceptionToErrorMapper`)
- [x] Injected entry point (`ToHttpResult` + `RunAsync`), with the exception path going
      through the same `IErrorHttpMapper`
- [x] `LimajHttpErrorOptions` + `AddLimajHttpErrors(...)` (no `IProblemDetailsWriter` registration)
- [x] Static facades delegate to the default mapping in `V2`, with characterization tests green
- [x] Mapped-error log level by resulting status; stable `EventId`s via `LoggerMessage`

### Phase 3 — Problem Details format selector (3.0.0, DA-003)
- [x] `LimajProblemDetailsFormat { V2, V3 }`, default `V2`
- [x] `V3`: `extensions.code` everywhere (incl. validation), 404/409 via `Results.Problem`,
      `detail = Message`, `title` by status
- [x] Contract suite: `ErrorType` × format, with and without `AddProblemDetails`

### Phase 4 — Exception details option (3.0.0, DA-004)
- [x] `bool? IncludeExceptionDetails` (`null` = current environment read, shared by facade and mapper)
- [x] Fixed, data-free startup `Warning` when `true`
- [x] Tests: 3 states, no stack-trace leak, startup warning

### Phase 5 — `Error` additions (3.0.0, DA-007, DA-008, DA-010)
- [x] `TimeSpan? RetryAfter` (`init`, outside the positional constructor) + `Retry-After` header
- [x] `Details` outside Validation behind an opt-in; never on 5xx
- [x] `[Obsolete]` on `Error.HttpStatusCode` without a binary break (source compatibility only
      since 3.0.0 — see the DA-010 note) (validate the positional-record
      mechanics); `#pragma` where the framework still reads it

### Phase 6 — Security fixes (3.0.0, DA-005)
- [x] Fixed code (`unexpected_error`) instead of `ex.Source` on the 500 body
- [x] Generic message for `Result.Unexpected` outside Development + temporary
      `[Obsolete]` opt-out `ExposeUnexpectedResultMessage`
- [x] `Validation` with `HttpStatusCode` keeps its `errors`
- [x] Tests for the three fixes

### Phase 7 — Documentation and release (3.0.0)
- [x] `packages/Limaj.Framework.Web/README.md`:
  - [x] `AddLimajHttpErrors`, `IErrorHttpMapper` (incl. 422 by `Code` until 4.0.0)
  - [x] `V2`/`V3`, and the `CustomizeProblemDetails` caveat under `V2`
  - [x] the `IProblemDetailsWriter` registration order
  - [x] the `OnRejected` recipe for the ASP.NET rate limiter
  - [x] the `IncludeExceptionDetails` guidance for Azure Functions
- [x] `CHANGELOG.md`: "Added" (DA-002/003/004/006/007/008), "Deprecated" (DA-010),
      "Security" (DA-005), and a note on the new log levels
- [x] Cut the 3.0.0 tag (stable release procedure from `done/nuget-package-publishing-pipeline.md`)

### Phase 8 — 4.0.0 (major, DA-001, DA-013)
- [ ] Default format → `V3` (`V2` kept as the migration value)
- [ ] `IncludeExceptionDetails` → `bool`, default `false`; remove every environment-name read
- [ ] `ErrorType.BusinessRule = 8` + `Result.BusinessRule(...)` + 422 mapping; update the
      closed-set test to 8 values (intentional rule change, DA-009)
- [ ] Remove `Error.HttpStatusCode` and its precedence branch in the default mapping
- [ ] Remove the temporary `ExposeUnexpectedResultMessage` opt-out
- [ ] Remove `IUserIdentityGateway`'s obsolete members (`GetCurrentUserIdAsync`,
      `IsAuthenticatedAsync` — `typed-user-principal` DA-006, DA-013)
- [ ] `CHANGELOG.md` migration notes:
  - [ ] format default, and how to pin `V2`
  - [ ] CS8509 on exhaustive switches
  - [ ] `HttpStatusCode` → `IErrorHttpMapper`/`BusinessRule`
  - [ ] `IncludeExceptionDetails` default
  - [ ] `IUserIdentityGateway` obsolete members → `GetCurrentPrincipalAsync`
- [ ] Cut the 4.0.0 tag

## Related

- Supersedes in part DA-003 of `done/test-foundation-and-persistence-error-fixes.md`
  (`ErrorType` closed at 7 → 8 in 4.0.0; `HttpStatusCode` escape hatch deprecated in 3.0.0).
- Extends DA-004 of `done/rename-functions-to-web.md` (generic 500 message) to the `Result`
  path.
- Separate follow-ups from the same consumer request: `done/result-core-package-split.md`
  (A3) and `done/typed-user-principal.md` (A4).
