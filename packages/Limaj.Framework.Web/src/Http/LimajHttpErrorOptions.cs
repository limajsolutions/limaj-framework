namespace Limaj.Framework.Web.Http;

/// <summary>
/// Options for the injected error pipeline registered by
/// <see cref="HttpErrorsServiceCollectionExtensions.AddLimajHttpErrors"/>. The static facades
/// (<see cref="ResultExtensions"/>, <see cref="RequestRunner"/>, <see cref="ExceptionExtensions"/>)
/// always behave as a default instance of this class.
/// </summary>
public sealed class LimajHttpErrorOptions
{
    /// <summary>The Problem Details contract to write (DA-003). Default <see cref="LimajProblemDetailsFormat.V2"/>.</summary>
    public LimajProblemDetailsFormat Format { get; set; } = LimajProblemDetailsFormat.V2;

    /// <summary>
    /// Whether an unhandled exception's <see cref="Exception.Message"/> (and an
    /// <c>ErrorType.Unexpected</c> result's message) reaches the client (DA-004/DA-005).
    /// <c>null</c> keeps the 2.0 behavior: exposed only when <c>ASPNETCORE_ENVIRONMENT</c> is
    /// <c>Development</c>. <c>true</c>/<c>false</c> always win. Even with <c>true</c>, only the
    /// message is exposed — never the stack trace, inner exceptions or <c>Exception.Data</c>.
    /// <c>true</c> logs a startup warning.
    /// </summary>
    public bool? IncludeExceptionDetails { get; set; }

    /// <summary>
    /// Whether <c>Error.Details</c> is sent (as <c>details</c>) for error types other than
    /// Validation (DA-008). Off by default, because products may hold internal or personal data
    /// there. Never applied to a 5xx response, even when on.
    /// </summary>
    public bool IncludeDetailsOutsideValidation { get; set; }

    /// <summary>
    /// Temporary opt-out of the DA-005 fix: when <c>true</c>, a 5xx <c>ErrorType.Unexpected</c>
    /// result sends its own message to the client in every environment, as in 2.0.
    /// </summary>
    [Obsolete("Temporary opt-out of the 2.1.0 security fix (DA-005), removed in 3.0.0. Use an ErrorType other than Unexpected for a message meant for the client.")]
    public bool ExposeUnexpectedResultMessage { get; set; }

    internal bool ResolveIncludeExceptionDetails(Func<bool> isDevelopmentEnvironment) =>
        IncludeExceptionDetails ?? isDevelopmentEnvironment();
}
