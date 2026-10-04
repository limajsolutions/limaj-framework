using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Web.Http;

/// <summary>
/// The error pipeline's log events, with stable <see cref="EventId"/>s (DA-006). Client-error
/// (4xx) events are numbered strictly below server-error (5xx) events. The ids are part of the
/// package's documented contract (README), so alerts can key on them.
/// </summary>
internal static partial class HttpErrorLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "ExceptionDetailsExposureEnabled",
        Level = LogLevel.Warning,
        Message = "LimajHttpErrorOptions.IncludeExceptionDetails is true: unhandled exception messages are returned to HTTP clients. Do not enable this in production.")]
    public static partial void ExceptionDetailsExposureEnabled(ILogger logger);

    [LoggerMessage(
        EventId = 4000,
        EventName = "ValidationFailed",
        Level = LogLevel.Warning,
        Message = "Validation error in {Operation}: {Message}")]
    public static partial void ValidationFailed(ILogger logger, string operation, string message);

    [LoggerMessage(
        EventId = 4001,
        EventName = "ResourceNotFound",
        Level = LogLevel.Warning,
        Message = "Not found in {Operation}: {Resource} ({Message})")]
    public static partial void ResourceNotFound(ILogger logger, string operation, string resource, string message);

    [LoggerMessage(
        EventId = 4002,
        EventName = "Conflict",
        Level = LogLevel.Warning,
        Message = "Conflict in {Operation}: {Message}")]
    public static partial void Conflict(ILogger logger, string operation, string message);

    [LoggerMessage(
        EventId = 4003,
        EventName = "MappedClientError",
        Level = LogLevel.Warning,
        Message = "Mapped error in {Operation}: {Code} ({StatusCode})")]
    public static partial void MappedClientError(ILogger logger, Exception exception, string operation, string code, int statusCode);

    [LoggerMessage(
        EventId = 5000,
        EventName = "MappedServerError",
        Level = LogLevel.Error,
        Message = "Mapped error in {Operation}: {Code} ({StatusCode})")]
    public static partial void MappedServerError(ILogger logger, Exception exception, string operation, string code, int statusCode);

    [LoggerMessage(
        EventId = 5001,
        EventName = "UnhandledException",
        Level = LogLevel.Error,
        Message = "Unhandled error in {Operation} (source: {Source})")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string operation, string? source);
}
