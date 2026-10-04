# Typed, extensible user principal for IUserIdentityGateway

**Status:** Done (implementation — functional sign-off is the user's decision)
**Última revisão:** 2026-10-04

## Context

This comes from the same consumer request that produced `web-error-extensibility.md`, where it
was listed as item **A4**, desirable and low priority.

`IUserIdentityGateway` (`packages/Limaj.Framework.Abstractions/src/Identity/`) returns only a
`string?` user id today. The consumer asked for a typed principal that products can extend.

In the `/flow` session of 2026-10-04 this item was taken out of the error-extensibility point
by consensus, because it is unrelated to error handling. It was then mediated in its own round
of the `/flow` execution run of 2026-10-04 (DA-001…DA-007 below).

## Initial positions (from that session, not decisions)

- **Architect.** Accept it as a separate, low-priority piece of work.
  - **Additive shape (minor):** a new interface (e.g. `ICurrentPrincipal<TPrincipal>`) or a
    default interface method.
  - **Breaking shape (major):** adding an abstract member to `IUserIdentityGateway` breaks
    every implementer.
  - **Constraint:** it needs its own design so that claims or host types do not leak into
    `Application`, as the package layering in `CLAUDE.md` requires.
- **Analyst.** Out of this point; low priority.
  - **Privacy:** a typed principal tends to carry personal data (e-mail, name, claims) into the
    application layer and into logs, so it needs its own privacy analysis.
  - **Versioning:** a new contract next to the current one is minor; changing
    `IUserIdentityGateway`'s signature is major.

## Architectural decisions

Settled in the `/flow` execution run of 2026-10-04 (1 round + 1 counter-argument; the
question still diverging after it was decided provisionally by the architect, for the user to
confirm or revert on PR #1). Implemented after `result-core-package-split`.

- **DA-001 — Implemented in 3.0.0.** *(user)* Breaking changes are allowed; it ships with PR #1
  (`web-error-extensibility` DA-013).

- **DA-002 — Shape: a base principal plus a generic gateway, in Abstractions.** *(consensus)*
  ADR candidate.
  - Everything lives in `Limaj.Framework.Abstractions.Identity` (not in `Limaj.Framework.Core`:
    identity is not result flow).
  - Base type `UserPrincipal`, which products extend (e.g. `AcmePrincipal : UserPrincipal` with
    a `TenantId`).
  - `IUserIdentityGateway` changes: `Task<UserPrincipal?> GetCurrentPrincipalAsync(CancellationToken
    cancellationToken = default)` — one snapshot, which fixes today's possible disagreement
    between `IsAuthenticatedAsync` and `GetCurrentUserIdAsync` in one operation.
  - `IUserIdentityGateway<TPrincipal> : IUserIdentityGateway where TPrincipal : UserPrincipal`
    declares `new Task<TPrincipal?> GetCurrentPrincipalAsync(...)` and implements the base member
    explicitly by delegating to it (`Task<T>` is not covariant). The non-generic base lets
    framework or generic Application code read the user id without knowing the product type.
  - **No host adapter** in the framework: which claim is the user id (`sub`, `oid`,
    `NameIdentifier`, Easy Auth headers) is an identity-provider decision, and a wrong default
    is a security and privacy bug. The Web README gets an illustrative `ClaimsPrincipal` →
    principal recipe for the product's host. No public identity contract exposes
    `System.Security.Claims` or ASP.NET Core types.

- **DA-003 — `null` means anonymous; a non-null principal means authenticated.**
  *(provisional — decided by architect; other view: the gateway never returns null — a
  `UserPrincipal(bool isAuthenticated, string? userId)` with `IsAuthenticated` and a
  `static Anonymous` instance; to revert: return non-null `Task<UserPrincipal>`/`Task<TPrincipal>`,
  add `bool IsAuthenticated`, `static UserPrincipal Anonymous` and a ctor
  `(bool isAuthenticated, string? userId)` rejecting `(false, non-null)`, and change the
  obsolete members to read `principal.IsAuthenticated`)* ADR candidate.
  - `GetCurrentPrincipalAsync` returns `null` exactly when there is no authenticated caller.
  - `public class UserPrincipal { public UserPrincipal(string? userId); public string? UserId { get; }
    [MemberNotNullWhen(true, nameof(UserId))] public bool HasUserId { get; } public sealed override string ToString(); }`
    — a class, get-only properties (no `init`, no positional record), so `with` cannot bypass
    validation. No `IsAuthenticated` property (redundant) and no `Anonymous` singleton.
  - The ctor throws `ArgumentException` when `userId` is non-null and empty or whitespace.
  - `UserId == null` on a non-null principal = an **authenticated non-user caller**
    (service-to-service, client credentials). Ownership checks against `UserId` then fail closed.
  - **Why:** with a generic `TPrincipal`, a base `Anonymous` cannot be returned as `TPrincipal`,
    so every derived principal (e.g. with a required `TenantId`) would have to invent a
    meaningless anonymous instance; null avoids that and makes the invalid `(false, id)` state
    impossible. Nullable reference types make callers handle anonymous at compile time.
  - Background jobs with no request: the product's gateway decides (e.g. a non-user principal).
    A first-class "system" kind is deferred; the docs mention it.

- **DA-004 — Data: only `UserId`.** *(consensus)*
  - `UserId`: an opaque, stable identifier assigned by the identity provider; never an e-mail or
    a name.
  - Tenant id, roles/permissions, e-mail and name are left to product-derived principals: the
    framework has no tenant concept or authorization, and e-mail/name identify a person directly.
  - **Privacy:** `UserId` is pseudonymous data, still personal data (LGPD art. 5 I, art. 13 §4;
    GDPR Recital 26); data minimization applies (LGPD art. 6 III; GDPR art. 5(1)(c)). The XML
    docs and README say: carry the minimum, prefer identifiers to personal data.

- **DA-005 — Kept out of logs, errors and responses.** *(consensus)*
  - `UserPrincipal.ToString()` is `sealed` and redacting (`{TypeName} { [redacted] }`): neither
    `UserId` nor any derived member value appears in `$"{p}"`, log placeholders or exception
    interpolation; logging the id is always an explicit choice of the caller.
  - The ctor's `ArgumentException` does not echo the rejected value.
  - No framework code logs a principal or puts it (or `UserId`) into `Error`, `Error.Details`,
    exception messages or its log events; Forbidden/Unauthorized errors do not echo the id.
  - Docs for products: `UserId` is personal data; destructuring (`{@Principal}`) and JSON
    serialization bypass `ToString`, so never destructure/serialize a principal into logs or
    return it raw from an endpoint; logs with user ids fall under data-subject requests and
    retention; the gateway is scoped per request and never caches a principal in a singleton;
    a cross-user access returns `NotFoundException` with no user id in the message.

- **DA-006 — Current members become `[Obsolete]` default members, removed in 4.0.0.** *(consensus)*
  - On the non-generic `IUserIdentityGateway`:
    `[Obsolete("Use GetCurrentPrincipalAsync(). Removed in 4.0.0.")] Task<string?> GetCurrentUserIdAsync(ct)`
    → `(await GetCurrentPrincipalAsync(ct))?.UserId`; `IsAuthenticatedAsync(ct)` →
    `principal is not null`. Same 3.0.0-deprecates / 4.0.0-removes rule as
    `web-error-extensibility` DA-013; the removal is tracked in that epic's 4.0.0 phase.
  - Callers keep compiling with a warning; implementers implement only the new member.
  - **Migration note** (CHANGELOG, BREAKING): implement `GetCurrentPrincipalAsync` (or
    `IUserIdentityGateway<TPrincipal>`); **delete your own `GetCurrentUserIdAsync` /
    `IsAuthenticatedAsync` implementations** — they would silently override the delegating
    defaults with no compiler warning; move callers to the snapshot.
  - `CLAUDE.md`/README rule becomes "Application depends on `IUserIdentityGateway` /
    `IUserIdentityGateway<TPrincipal>`, never on `HttpContext` / `ClaimsPrincipal`".
    `GLOSSARY.md` gets "Principal" and "User id".

- **DA-007 — Tests (TDD, written first).** *(consensus)* In `Limaj.Framework.Abstractions.Tests`,
  with hand-written fakes (no mocks):
  - the ctor rejects `""` and `"  "`, accepts `null` and a value; `UserId` round-trips;
  - `ToString()` of the base and of a derived test principal carrying an e-mail contains neither
    the `UserId` nor the e-mail; the ctor exception does not echo the value;
  - the obsolete members map a null principal to `(null, false)`, a non-user principal to
    `(null, true)` and a user principal to `(id, true)` (CS0618 suppressed locally), and pass the
    `CancellationToken` through;
  - the generic gateway's explicit base implementation returns the same instance (and null for
    anonymous);
  - `Architecture.Tests`: Abstractions and Application do not depend on `System.Security.Claims`
    or `Microsoft.AspNetCore`.

## Proposed structure by layer

```
packages/
  Limaj.Framework.Abstractions/
    Limaj.Framework.Abstractions.csproj         (Description mentions the typed user principal — DA-002)
    src/Identity/
      UserPrincipal.cs                          (new — base principal: ctor(string? userId), UserId, HasUserId,
                                                 sealed redacting ToString — DA-003, DA-004, DA-005)
      IUserIdentityGateway.cs                   (GetCurrentPrincipalAsync; GetCurrentUserIdAsync/IsAuthenticatedAsync
                                                 become [Obsolete] default members; + IUserIdentityGateway<TPrincipal>
                                                 with `new` GetCurrentPrincipalAsync and the explicit base
                                                 implementation — DA-002, DA-003, DA-006)
    Limaj.Framework.Abstractions.Tests/
      UserPrincipalTests.cs                     (new — DA-007)
      UserIdentityGatewayTests.cs               (new — hand-written fake gateways — DA-007)
  Limaj.Framework.Architecture.Tests/
    PackageDependencyDirectionTests.cs          (Abstractions and Application never depend on
                                                 System.Security.Claims or Microsoft.AspNetCore — DA-002, DA-007)
  Limaj.Framework.Web/README.md                 (illustrative ClaimsPrincipal → principal recipe for the product's
                                                 host; privacy notes — DA-002, DA-004, DA-005)
  PACKAGE_README.md                             (Abstractions row mentions the typed principal — DA-002)
CLAUDE.md, README.md                            (rule: Application depends on IUserIdentityGateway /
                                                 IUserIdentityGateway<TPrincipal>, never on HttpContext / ClaimsPrincipal — DA-006)
GLOSSARY.md                                     ("Principal" and "User id" — DA-006)
CHANGELOG.md                                    (BREAKING entry + migration note, "Upgrading from 2.x to 3.0.0" line,
                                                 Deprecated entry — DA-006)
```

No new package and no new reference: everything lives in `Limaj.Framework.Abstractions.Identity`
(DA-002). No host adapter is added to `Limaj.Framework.Web`, and no identity contract exposes
`System.Security.Claims` or ASP.NET Core types.

## Test strategy

TDD, tests written first, with hand-written fakes and no mocks (DA-007):

- **`UserPrincipalTests`** (Abstractions.Tests): the ctor rejects `""` and `"  "` with an
  `ArgumentException` that does not echo the value, and accepts `null` and a value; `UserId`
  round-trips and `HasUserId` follows it; `ToString()` of the base and of a derived test
  principal carrying an e-mail contains neither the `UserId` nor the e-mail.
- **`UserIdentityGatewayTests`** (Abstractions.Tests): the `[Obsolete]` default members, called
  with CS0618 suppressed locally, map a null principal to `(null, false)`, a non-user principal
  to `(null, true)` and a user principal to `(id, true)`, and pass the `CancellationToken`
  through; the generic gateway's explicit base implementation returns the same instance, and
  null for anonymous.
- **Architecture tests:** Abstractions and Application do not depend on
  `System.Security.Claims` or `Microsoft.AspNetCore`.
- **No regression:** every other suite stays green unchanged (no framework code calls the
  gateway today).

## Implementation notes

Choices made where the decisions above left the mechanics open:

- **One file for both gateway interfaces:** `IUserIdentityGateway` and
  `IUserIdentityGateway<TPrincipal>` live in `IUserIdentityGateway.cs`, as `Error`/`ErrorType`
  share `Error.cs`, to avoid an awkward generic file name.
- **Product-facing privacy notes:** Abstractions has no README, so the DA-004/DA-005 notes for
  products live in the Web README's identity section (next to the recipe), in the XML docs of
  `UserPrincipal`/`IUserIdentityGateway`, and in short form in `CLAUDE.md`, `README.md` and
  `GLOSSARY.md`.
- **Non-echoing exception test:** the only rejected values are empty/whitespace, so the test
  asserts the message is identical for `""` and `" \t "` (independent of the value) rather than
  searching for the value in it.
- **Obsolete-member cases** use `InlineData` (has-principal flag + user id) instead of
  `TheoryData<UserPrincipal?, …>`, which xUnit cannot serialize.

## Phase checklist

### Phase 1 — Decide
- [x] Mediate DA-001 in `/flow`, then fill in the structure, tests and implementation phases

### Phase 2 — Tests first (DA-007)
- [x] `UserPrincipalTests`: ctor validation and non-echoing exception, `UserId`/`HasUserId`,
      redacting `ToString` of the base and of a derived principal with an e-mail
- [x] `UserIdentityGatewayTests`: obsolete members' mapping and `CancellationToken` pass-through;
      the generic gateway's explicit base implementation (same instance, null)
- [x] Architecture tests: Abstractions and Application do not depend on `System.Security.Claims`
      or `Microsoft.AspNetCore`

### Phase 3 — Identity contracts (DA-002, DA-003, DA-004, DA-005, DA-006)
- [x] `UserPrincipal` (class, get-only, validating ctor, `HasUserId` with `MemberNotNullWhen`,
      sealed redacting `ToString`), XML docs with the privacy notes
- [x] `IUserIdentityGateway.GetCurrentPrincipalAsync`; `GetCurrentUserIdAsync`/`IsAuthenticatedAsync`
      as `[Obsolete("Use GetCurrentPrincipalAsync(). Removed in 4.0.0.")]` default members
- [x] `IUserIdentityGateway<TPrincipal>` with `new GetCurrentPrincipalAsync` and the explicit base
      implementation delegating to it; Abstractions `Description` updated

### Phase 4 — Documentation (DA-002, DA-004, DA-005, DA-006)
- [x] Web README: illustrative `ClaimsPrincipal` → principal recipe and the privacy notes for products
- [x] `CLAUDE.md` and `README.md` identity rule; `PACKAGE_README.md`; `GLOSSARY.md` "Principal" and
      "User id"
- [x] `CHANGELOG.md`: BREAKING entry with the migration note, "Upgrading from 2.x to 3.0.0" line,
      Deprecated entry; check the 4.0.0 removal is in `web-error-extensibility` Phase 8
- [x] Validate: `dotnet build` (no new warnings), `dotnet test` green
