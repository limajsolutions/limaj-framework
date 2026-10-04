# Limaj.Framework

Reusable, domain-agnostic building blocks for .NET 10 / Azure Functions SaaS backends.

| Package | Purpose | Depends on |
|---|---|---|
| `Limaj.Framework.Core` | The dependency-free result/error contract: `Result`/`Error`, framework exceptions, `IExceptionToErrorMapper` | — |
| `Limaj.Framework.Abstractions` | Pure contracts and types: `BaseEntity`, repository/unit of work, identity gateway | — |
| `Limaj.Framework.Application` | Application-layer base services built on the abstractions | `Abstractions` + `Core` |
| `Limaj.Framework.Persistence.EFCore` | EF Core base repository (soft delete), unit of work, base entity configuration, UTC/retry helpers | `Abstractions` + EF Core |
| `Limaj.Framework.Web` | HTTP mapping of `Result` → status codes, `RequestRunner` exception bridge | `Core` + ASP.NET Core |

The 5 packages ship in lockstep — always reference the same version of all of them.
`Limaj.Framework.Core` was split out of `Abstractions` in 3.0.0 and starts at that version.

```bash
dotnet add package Limaj.Framework.Core
dotnet add package Limaj.Framework.Abstractions
dotnet add package Limaj.Framework.Application
dotnet add package Limaj.Framework.Persistence.EFCore
dotnet add package Limaj.Framework.Web
```

Documentation, architecture rules and source: https://github.com/limajsolutions/limaj-framework
