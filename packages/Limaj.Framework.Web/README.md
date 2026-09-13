# Limaj.Framework.Web

Generic, host-agnostic HTTP pipeline: `RequestRunner`, `ResultExtensions`, and
`ExceptionExtensions` use exclusively `Microsoft.AspNetCore.Http.HttpRequest`/`IResult`,
the same types used both by the Azure Functions isolated worker (via ASP.NET Core
integration) and by Minimal API. No Azure Functions-specific type is referenced here.
