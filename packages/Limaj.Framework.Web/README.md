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
