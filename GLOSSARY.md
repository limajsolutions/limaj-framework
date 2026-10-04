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

## Identity

**Principal**:
The authenticated caller of the current operation, as a `UserPrincipal` (or a product type derived from it) returned by `IUserIdentityGateway.GetCurrentPrincipalAsync`. No principal (`null`) means there is no authenticated caller; a principal without a user id is an authenticated non-user caller (service-to-service, client credentials). It carries the minimum the product needs and never prints its values (`ToString` is redacted).
_Avoid_: Current user (for a non-user caller), claims, ClaimsPrincipal (in Application code), identity

**User id**:
The opaque, stable identifier the identity provider assigns to a user (`UserPrincipal.UserId`). It is pseudonymous personal data: never an e-mail or a name, never logged, put in an error or returned without a reason.
_Avoid_: E-mail or username (as the id), login, subject (outside the identity provider's own vocabulary)

## Packages

**Result core**:
The `Limaj.Framework.Core` package: the dependency-free result/error contract every package and product shares (`Result`/`Result<T>`, `Error`/`ErrorType`, the framework exceptions and `IExceptionToErrorMapper`). Its charter is result/error contract only; any other type needs a DA.
_Avoid_: Core (for any type outside the result/error contract), Abstractions (for these types)
