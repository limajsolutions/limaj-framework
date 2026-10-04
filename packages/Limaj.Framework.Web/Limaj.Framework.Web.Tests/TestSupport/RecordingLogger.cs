using Microsoft.Extensions.Logging;

namespace Limaj.Framework.Web.Tests.TestSupport;

/// <summary>A fake <see cref="ILogger"/> that keeps every entry for assertions.</summary>
internal sealed class RecordingLogger : ILogger, ILoggerProvider
{
    public List<RecordedLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        Entries.Add(new RecordedLogEntry(logLevel, eventId, exception, formatter(state, exception), properties));
    }

    ILogger ILoggerProvider.CreateLogger(string categoryName) => this;

    void IDisposable.Dispose()
    {
    }
}

internal sealed record RecordedLogEntry(
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> Properties);
