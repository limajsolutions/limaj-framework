using Limaj.Framework.Core;
using Limaj.Framework.Core.Errors;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The framework's built-in exception bridge as a public <see cref="IExceptionToErrorMapper"/>
/// (DA-006): <see cref="DomainValidationException"/> -> Validation,
/// <see cref="NotFoundException"/> -> NotFound, <see cref="ConflictException"/> -> Conflict;
/// <c>null</c> for anything else. The error pipeline runs it before the host's
/// <see cref="IExceptionToErrorMapper"/>, so a host catch-all never shadows it. A host that
/// wants another order composes its own chain with this class, or registers a subclass in its
/// place (see the package README).
/// </summary>
public class BuiltInExceptionToErrorMapper : IExceptionToErrorMapper
{
    public virtual Error? Map(Exception exception) => exception switch
    {
        DomainValidationException validationException => new Error(
            "validation_failed",
            validationException.Message,
            ErrorType.Validation,
            new Dictionary<string, string[]>(validationException.Errors)),

        NotFoundException notFoundException => new Error(
            notFoundException.Resource,
            notFoundException.Message,
            ErrorType.NotFound),

        ConflictException conflictException => new Error(
            "conflict",
            conflictException.Message,
            ErrorType.Conflict),

        _ => null
    };
}
