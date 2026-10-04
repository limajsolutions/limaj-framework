# Limaj.Framework.Web

Generic, host-agnostic HTTP pipeline: `RequestRunner`, `ResultExtensions`, and
`ExceptionExtensions` use exclusively `Microsoft.AspNetCore.Http.HttpRequest`/`IResult`,
the same types used both by the Azure Functions isolated worker (via ASP.NET Core
integration) and by Minimal API. No Azure Functions-specific type is referenced here.

## Error responses: two entry points

| Entry point | How errors are written |
|---|---|
| Static facades: `result.ToHttpResult(...)`, `RequestRunner.RunAsync(...)`, `ex.ToHttpResult(...)` | Always `DefaultErrorHttpMapper` with default options (`V2` format). Signatures and concrete result types are unchanged from 2.0. |
| Injected `IHttpResultResponder` (registered by `AddLimajHttpErrors`) | The registered `IErrorHttpMapper`, with your `LimajHttpErrorOptions`. |

Both entry points send every error down one path. An exception becomes an `Error`: first the
built-in exceptions, then the host's `IExceptionToErrorMapper`, then Unexpected. A failed
`Result` already carries one. The `Error` then goes to the `IErrorHttpMapper`.

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddLimajHttpErrors(options =>
{
    options.Format = LimajProblemDetailsFormat.V3;
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
});

app.MapPost("/orders", (CreateOrder command, OrderService service, IHttpResultResponder responder) =>
    responder.RunAsync(
        async () => responder.ToHttpResult(await service.CreateAsync(command), order => Results.Created($"/orders/{order.Id}", order)),
        "CreateOrder"));
```

`AddLimajHttpErrors` registers `IHttpResultResponder` (scoped), `IErrorHttpMapper` →
`DefaultErrorHttpMapper`, `BuiltInExceptionToErrorMapper` and `LimajHttpErrorOptions`. It
adds each one only if it is not registered yet. It calls neither `AddProblemDetails` nor
registers an `IProblemDetailsWriter` (see the registration order below).

## Custom statuses: `IErrorHttpMapper`

`DefaultErrorHttpMapper` is public and has one virtual method per `ErrorType`
(`MapValidation`, `MapNotFound`, …), plus `MapWithStatusCode(error, statusCode)` for any
other status. Inherit or decorate it and register it as `IErrorHttpMapper`. A registration
made before or after `AddLimajHttpErrors` wins either way.

Until 4.0.0 adds `ErrorType.BusinessRule`, use this mapper to answer **422** (or 402, 410,
412, 503…), keyed on `Error.Code`. This replaces the deprecated `Error.HttpStatusCode`:

```csharp
public sealed class ProductErrorHttpMapper(IOptions<LimajHttpErrorOptions> options)
    : DefaultErrorHttpMapper(options)
{
    public override IResult Map(Error error) => error.Code switch
    {
        "order_already_shipped" => MapWithStatusCode(error, StatusCodes.Status422UnprocessableEntity),
        "plan_limit_exceeded" => MapWithStatusCode(error, StatusCodes.Status402PaymentRequired),
        _ => base.Map(error)
    };
}

builder.Services.AddSingleton<IErrorHttpMapper, ProductErrorHttpMapper>();
```

`MapWithStatusCode` writes in the configured format. A Validation error keeps its `errors`.

### Exception mapping order

`DomainValidationException`, `NotFoundException` and `ConflictException` are always mapped
first (`BuiltInExceptionToErrorMapper`), so a catch-all host `IExceptionToErrorMapper` cannot
turn them into something else. To run your own mapper first, register a subclass in place of
the built-in one:

```csharp
public sealed class HostFirstExceptionMapper(ProductExceptionMapper product) : BuiltInExceptionToErrorMapper
{
    public override Error? Map(Exception exception) => product.Map(exception) ?? base.Map(exception);
}

builder.Services.AddSingleton<ProductExceptionMapper>();
builder.Services.AddSingleton<BuiltInExceptionToErrorMapper, HostFirstExceptionMapper>();
```

## Problem Details format: `V2` / `V3`

`LimajHttpErrorOptions.Format` selects the whole response contract. The default in 3.x is
`V2`; 4.0.0 makes `V3` the default and keeps `V2` as the value to pin during a migration.

| | `V2` (default in 3.x) | `V3` |
|---|---|---|
| `title` | `Error.Message` | the status's reason phrase (`Not Found`, `Unprocessable Entity`, …) |
| `detail` | `Error.Code` (none on validation responses) | `Error.Message` |
| `code` | absent | `Error.Code`, on every error response, validation included |
| `retryAfter` | absent | seconds, when `Error.RetryAfter` is set |
| 404 / 409 | `Results.NotFound/Conflict(ProblemDetails)`: `application/json`, outside `IProblemDetailsService` | `Results.Problem`, like every other error |

**Caveat under `V2`:** `CustomizeProblemDetails` is **not** global. It never reaches 404/409,
which also get no `traceId`, and it gets no `code` to read. Only `V3` puts every error
response through `IProblemDetailsService`.

An unexpected (5xx) error's message is replaced by `"An unexpected error occurred."` unless
exception details are enabled (below). `Error.Details` on any type other than Validation is
dropped. Set `IncludeDetailsOutsideValidation = true` to send it as `details`. It is never
sent on a 5xx response.

## Replacing the body format: `IProblemDetailsWriter` registration order

ASP.NET Core picks the first `IProblemDetailsWriter` that can write. A custom writer, such as
a `{ success, error }` envelope, therefore only replaces the format when you register it
**before** `AddProblemDetails`. Registered after it, the default writer wins:

```csharp
builder.Services.AddSingleton<IProblemDetailsWriter, EnvelopeProblemDetailsWriter>(); // first
builder.Services.AddProblemDetails();
builder.Services.AddLimajHttpErrors(options => options.Format = LimajProblemDetailsFormat.V3);
```

Use `V3` with a custom writer: it is the only format where every error reaches the writer,
with `code` in `ProblemDetails.Extensions`. `AddLimajHttpErrors` never registers a writer, so
it does not affect this order.

## `Retry-After`

Set `Error.RetryAfter` (`init`, transport-neutral). The default mapping adds a `Retry-After`
header in whole seconds, rounded up, when the value is > 0. It wraps the result in
`RetryAfterHttpResult`, whose `InnerResult` is the usual one. Under `V3` the body also carries
`retryAfter`:

```csharp
return Result.Fail(new Error("quota_exceeded", "Daily quota exceeded.", ErrorType.TooManyRequests)
{
    RetryAfter = TimeSpan.FromMinutes(5)
});
```

**ASP.NET Core rate limiter.** Its 429 is sent before your handler runs, so it never goes
through this package on its own. To give it the same body and header, map it in
`OnRejected`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        var error = new Error("rate_limited", "Too many requests.", ErrorType.TooManyRequests)
        {
            RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : null
        };
        var mapper = context.HttpContext.RequestServices.GetRequiredService<IErrorHttpMapper>();
        return new ValueTask(mapper.Map(error).ExecuteAsync(context.HttpContext));
    };
});
```

## Exception details: `IncludeExceptionDetails`

| Value | An unhandled exception's / a 5xx `ErrorType.Unexpected` result's message |
|---|---|
| `null` (default in 3.x) | sent only when `ASPNETCORE_ENVIRONMENT` is `Development` (2.0 behavior) |
| `true` | always sent; a fixed warning (EventId 1000) is logged at startup |
| `false` | never sent; the client gets `"An unexpected error occurred."` |

Even with `true`, only `Exception.Message` is sent: never the stack trace, inner exceptions
or `Exception.Data`. The 500's code is always `unexpected_error`. `ex.Source` and the full
exception only go to the log.

**Azure Functions (isolated worker):** `ASPNETCORE_ENVIRONMENT` is usually absent there (the
host uses `AZURE_FUNCTIONS_ENVIRONMENT`), so `null` behaves as "not Development". Set
`IncludeExceptionDetails` explicitly from your own configuration. The framework will not read
`AZURE_FUNCTIONS_ENVIRONMENT`, because 4.0.0 removes environment reads altogether (the option
becomes a `bool`, default `false`). The static facades have no options: they always behave as
`null`.

`ExposeUnexpectedResultMessage` (`[Obsolete]`, removed in 4.0.0) temporarily restores the
2.0 behavior of sending a 5xx `Result.Unexpected` message in every environment. For a message
meant for the client, use another `ErrorType`.

## Log events

Both entry points log through `LoggerMessage` with stable `EventId`s. A 4xx event id is always
below every 5xx event id.

| EventId | Name | Level | When |
|---|---|---|---|
| 1000 | `ExceptionDetailsExposureEnabled` | Warning | startup, `IncludeExceptionDetails = true` |
| 4000 | `ValidationFailed` | Warning | `DomainValidationException` |
| 4001 | `ResourceNotFound` | Warning | `NotFoundException` |
| 4002 | `Conflict` | Warning | `ConflictException` |
| 4003 | `MappedClientError` | Warning | host-mapped exception resulting in a 4xx (with the exception) |
| 5000 | `MappedServerError` | Error | host-mapped exception resulting in a 5xx (with the exception) |
| 5001 | `UnhandledException` | Error | unmapped exception (with the exception and `ex.Source`) |

The status is resolved from the `Error`: its type, or the deprecated `HttpStatusCode` when
set. A host `IErrorHttpMapper` that answers another status does not change the log level.

## Identity: mapping the host's user to a principal

Application code reads the caller through `IUserIdentityGateway` (or
`IUserIdentityGateway<TPrincipal>`) from `Limaj.Framework.Abstractions.Identity`, never through
`HttpContext` or `ClaimsPrincipal`. The framework ships **no** host adapter: which claim is the
user id (`sub`, `oid`, `NameIdentifier`, Easy Auth headers…) is your identity provider's
decision, and a wrong default would be a security and privacy bug. The product's host
implements the gateway. The recipe below is illustrative; adapt the claim names to your
provider.

```csharp
// Product code: a principal with what the product needs, and nothing more.
public sealed class AcmePrincipal(string? userId, string? tenantId) : UserPrincipal(userId)
{
    // null = an authenticated caller whose token carries no tenant: Application answers 403.
    public string? TenantId { get; } = tenantId;
}

// Product host: ClaimsPrincipal → principal. Claims types stay in the host.
// This recipe is for an API that authenticates JWT bearer ACCESS tokens (not ID tokens or cookie
// sessions), with JwtBearerOptions.MapInboundClaims = false; otherwise ASP.NET Core renames
// "sub", "scp", "roles"... to long URIs.
public sealed class HttpUserIdentityGateway(IHttpContextAccessor httpContextAccessor)
    : IUserIdentityGateway<AcmePrincipal>
{
    public Task<AcmePrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<AcmePrincipal?>(null); // null = no authenticated caller, nothing else
        }

        // An app-only (client-credentials) caller is authenticated but is not a user: UserId == null.
        // Its token can still carry "sub"/"oid" (the service principal or client), so it is detected
        // explicitly, never inferred from a missing user-id claim.
        var userId = IsAppOnlyCaller(user) ? null : user.FindFirst(UserIdClaim)?.Value;

        // A caller without a tenant claim is still a non-null principal (DA-003), with
        // TenantId == null, never null.
        var tenantId = user.FindFirst(TenantIdClaim)?.Value;

        return Task.FromResult<AcmePrincipal?>(new AcmePrincipal(userId, tenantId));
    }

    // The members below are PROVIDER-SPECIFIC: keep only your identity provider's version, never
    // mix providers, and verify them against real user and client-credentials access tokens from
    // your IdP before shipping.

    // Entra ID: the stable user id is "oid" (unique with "tid"); "sub" is pairwise per application.
    private const string UserIdClaim = "oid";
    private const string TenantIdClaim = "tid";

    // Entra ID: when the "idtyp" optional claim is enabled in the app registration, it decides
    // ("app" = app-only, "user" = delegated). Without it, fall back on "scp": a delegated access
    // token normally carries it and an app-only one does not (it carries app "roles", if
    // granted), so a missing "scp" counts as app-only. Either edge case (a delegated token
    // without "scp", an app-only token without roles) fails closed: no user id. The fallback
    // holds for access tokens only: ID tokens and cookie sessions carry no "scp".
    private static bool IsAppOnlyCaller(ClaimsPrincipal user)
        => user.FindFirst("idtyp")?.Value is { } tokenType
            ? tokenType == "app"
            : user.FindFirst("scp") is null;

    // Auth0 instead. The "sub" user id also fits generic OIDC providers, but the "gty" check is
    // Auth0-only: Okta, Keycloak and other providers do not emit "gty", so they need their own
    // app-only signal, or every client-credentials caller would pass as a user (fails open).
    // Auth0 client-credentials tokens carry gty = "client-credentials" (and
    // sub = "{client_id}@clients"); Auth0 user tokens have no "scp", so do not reuse the Entra
    // check. The tenant claim comes from Auth0 Organizations.
    // private const string UserIdClaim = "sub";
    // private const string TenantIdClaim = "org_id";
    // private static bool IsAppOnlyCaller(ClaimsPrincipal user)
    //     => user.HasClaim("gty", "client-credentials");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpUserIdentityGateway>();
builder.Services.AddScoped<IUserIdentityGateway<AcmePrincipal>>(sp => sp.GetRequiredService<HttpUserIdentityGateway>());
builder.Services.AddScoped<IUserIdentityGateway>(sp => sp.GetRequiredService<HttpUserIdentityGateway>());
```

In Application code, take one snapshot per operation:

```csharp
var principal = await identityGateway.GetCurrentPrincipalAsync(cancellationToken); // IUserIdentityGateway<AcmePrincipal>
if (principal is null)
{
    return Result<Order>.Unauthorized(); // 401: no authenticated caller
}

if (principal.TenantId is null)
{
    return Result<Order>.Forbidden(); // 403: authenticated, but the principal has no tenant
}

if (!principal.HasUserId || order.TenantId != principal.TenantId || order.OwnerId != principal.UserId)
{
    throw new NotFoundException("Order", order.Id); // no user id in the message
}
```

`GetCurrentPrincipalAsync` returns `null` exactly when there is no authenticated caller. A
non-null principal with `UserId == null` is an authenticated **non-user** caller
(service-to-service, client credentials), so ownership checks against `UserId` fail closed.
An authenticated caller whose principal lacks something the product needs (here, a tenant) is
still a non-null principal: Application answers `Forbidden` (403), never `Unauthorized` (401).
For background jobs with no request, your gateway decides what to return (for example a
non-user principal); the framework has no first-class "system" caller yet.

### Privacy

- `UserId` is the identity provider's opaque, stable identifier, never an e-mail or a name. It
  is pseudonymous data, which is still personal data (LGPD art. 5 I and art. 13 §4; GDPR
  Recital 26).
- Carry the minimum (LGPD art. 6 III; GDPR art. 5(1)(c)): prefer identifiers to personal data,
  and add e-mail, name or roles to a derived principal only when the product needs them.
- `UserPrincipal.ToString()` is sealed and returns `{TypeName} { [redacted] }`, so `$"{principal}"`
  and log placeholders leak nothing, in derived principals too. Structured-log destructuring
  (`{@Principal}`) and JSON serialization **bypass** `ToString`: never destructure or serialize
  a principal into logs, and never return it raw from an endpoint. Logging a user id is always
  an explicit choice, and logs that carry user ids fall under data-subject requests and your
  retention policy.
- Register the gateway as scoped (per request) and never cache a principal in a singleton.
- A cross-user access answers `NotFoundException` (404) with no user id in the message, so it
  reveals neither the resource's existence nor whose it is.
- The framework never logs a principal, nor puts it or its `UserId` into an `Error`,
  `Error.Details`, an exception message or a log event; its Forbidden/Unauthorized errors do
  not echo the id.

`GetCurrentUserIdAsync` and `IsAuthenticatedAsync` are `[Obsolete]` default members, derived
from `GetCurrentPrincipalAsync`, and are removed in 4.0.0. Do not implement them: a class's
own implementation silently replaces the default. They are reachable only through the
interface: a call through `IUserIdentityGateway` compiles with warning CS0618, while a call on
a variable typed as the concrete gateway class does not compile (CS1061).
