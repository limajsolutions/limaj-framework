# Limaj.Framework

Reusable, domain-agnostic building blocks for .NET 10 / Azure Functions SaaS backends.

| Package | Purpose | Depends on |
|---|---|---|
| `Limaj.Framework.Abstractions` | Pure contracts and types: `Result`/`Error`, `BaseEntity`, domain exceptions, identity gateway | — |
| `Limaj.Framework.Application` | Application-layer base services built on the abstractions | `Abstractions` |
| `Limaj.Framework.Persistence.EFCore` | EF Core base repository (soft delete), unit of work, base entity configuration, UTC/retry helpers | `Abstractions` + EF Core |
| `Limaj.Framework.Web` | HTTP mapping of `Result` → status codes, `RequestRunner` exception bridge | `Abstractions` + ASP.NET Core |

The 4 packages ship in lockstep — always reference the same version of all of them.

```bash
dotnet add package Limaj.Framework.Abstractions
dotnet add package Limaj.Framework.Application
dotnet add package Limaj.Framework.Persistence.EFCore
dotnet add package Limaj.Framework.Web
```

Documentation, architecture rules and source: https://github.com/limajsolutions/limaj-framework
