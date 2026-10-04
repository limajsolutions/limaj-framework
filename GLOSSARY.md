# Limaj Framework

Domain-agnostic building blocks for .NET SaaS backends. The terms below are the framework's
own vocabulary: the contract its packages expose to every product built on them.

## Error categories

**Validation error**:
A failure caused by the input itself (missing, malformed or out-of-range data); the caller fixes it by changing the input.
_Avoid_: Bad request, invalid data

**Business rule violation**:
A failure where the input is valid but a rule of the product forbids the operation in the current state; changing the input's shape does not fix it.
_Avoid_: Validation error (for this case), unprocessable, domain error

**Conflict**:
A failure caused by concurrent or duplicate state (the resource already exists, or changed since it was read); the caller may retry after refreshing that state.
_Avoid_: Business rule violation (for this case), duplicate error

## Packages

**Result core**:
The `Limaj.Framework.Core` package: the dependency-free result/error contract every package and product shares (`Result`/`Result<T>`, `Error`/`ErrorType`, the framework exceptions and `IExceptionToErrorMapper`). Its charter is result/error contract only; any other type needs a DA.
_Avoid_: Core (for any type outside the result/error contract), Abstractions (for these types)
