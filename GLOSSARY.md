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
