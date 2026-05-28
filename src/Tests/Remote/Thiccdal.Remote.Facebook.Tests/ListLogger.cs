using Microsoft.Extensions.Logging;

namespace Thiccdal.Remote.Facebook.Tests;

public sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    public bool Contains(LogLevel level, string messageFragment)
    {
        return _entries.Any(entry =>
            entry.Level == level &&
            entry.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    public sealed record LogEntry(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
