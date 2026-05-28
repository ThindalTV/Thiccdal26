using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Remote.Null;

namespace Thiccdal.Remote.Null.Tests;

public class NullRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingNullIntegration_ThenRegistersSharedNullServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{NullOptions.SectionName}:PlatformName"] = "Offline",
                [$"{NullOptions.SectionName}:AuthorizationUrl"] = "https://example.test/null"
            })
            .Build();

        services.AddNullIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        var nullConnection = provider.GetRequiredService<NullPlatformConnection>();
        var platformConnection = provider.GetRequiredService<IPlatformConnection>();
        var chatSource = provider.GetRequiredService<IChatSource>();
        var streamTarget = provider.GetRequiredService<IStreamTarget>();
        var eventSource = provider.GetRequiredService<IEventSource>();
        var platformEventSource = provider.GetRequiredService<IPlatformEventSource>();
        var integrationMonitor = provider.GetRequiredService<IIntegrationConnectionMonitor>();
        var options = provider.GetRequiredService<IOptions<NullOptions>>().Value;

        Assert.Same(nullConnection, platformConnection);
        Assert.Same(nullConnection, chatSource);
        Assert.Same(nullConnection, streamTarget);
        Assert.Same(nullConnection, eventSource);
        Assert.Same(nullConnection, platformEventSource);
        Assert.Same(nullConnection, integrationMonitor);
        Assert.Equal("Offline", options.PlatformName);
        Assert.Equal("https://example.test/null", options.AuthorizationUrl);
    }

    [Fact]
    public async Task WhenUsingNullPlatform_ThenOperationsAreLoggedWithoutEmittingTraffic()
    {
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new TestLoggerProvider(logEntries));
        });

        var connection = new NullPlatformConnection(
            Options.Create(new NullOptions
            {
                PlatformName = "Offline",
                AuthorizationUrl = "https://example.test/null"
            }),
            loggerFactory.CreateLogger<NullPlatformConnection>());

        int platformEvents = 0;
        int chatEvents = 0;
        int connectionChanges = 0;

        connection.OnPlatformEventReceived += (_, _) => platformEvents++;
        connection.OnChatMessageRecieved += (_, _) => chatEvents++;
        connection.ConnectionChanged += (_, _) => connectionChanges++;

        string authorizationUrl = connection.GetAuthorizationUrl();
        await connection.Connect();
        await connection.SendMessage("hello world");
        await connection.RefreshConnectionState();
        await connection.Disconnect();

        Assert.Equal("https://example.test/null", authorizationUrl);
        Assert.False(connection.Connected);
        Assert.False(connection.IsConnected);
        Assert.Equal(0, platformEvents);
        Assert.Equal(0, chatEvents);
        Assert.Equal(2, connectionChanges);
        Assert.Contains(logEntries, entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Resolving authorization URL", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Connecting null platform", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Discarding outbound null platform message", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Refreshing null platform", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Disconnecting null platform", StringComparison.Ordinal));
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly IList<LogEntry> _entries;

        public TestLoggerProvider(IList<LogEntry> entries)
        {
            _entries = entries;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_entries, categoryName);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger : ILogger
    {
        private readonly IList<LogEntry> _entries;
        private readonly string _categoryName;

        public TestLogger(IList<LogEntry> entries, string categoryName)
        {
            _entries = entries;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoOpDisposable.Instance;
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
            _entries.Add(new LogEntry(_categoryName, logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(string CategoryName, LogLevel LogLevel, string Message);

    private sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new NoOpDisposable();

        public void Dispose()
        {
        }
    }
}
