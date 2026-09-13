# Limaj.Framework.Web

Pipeline HTTP genérico, agnóstico de host: `RequestRunner`, `ResultExtensions` e
`ExceptionExtensions` usam exclusivamente `Microsoft.AspNetCore.Http.HttpRequest`/`IResult`,
os mesmos tipos usados tanto pelo Azure Functions isolated worker (via integração ASP.NET
Core) quanto pelo Minimal API. Nenhum tipo específico de Azure Functions é referenciado aqui.
