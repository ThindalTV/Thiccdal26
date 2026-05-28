using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.TikTok;
using Thiccdal.Remote.TikTok;

namespace Thiccdal.Remote.TikTok.Tests;

public class TikTokRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingTikTokIntegration_ThenRegistersSharedTikTokServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TikTokOptions.SectionName}:IsEnabled"] = "false",
                [$"{TikTokOptions.SectionName}:CreatorId"] = "test-creator-123",
                [$"{TikTokOptions.SectionName}:AccessToken"] = ""
            })
            .Build();

        services.AddTikTokIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        var tikTokConnection = provider.GetRequiredService<TikTokService>();
        var platformConnection = provider.GetRequiredService<IPlatformConnection>();
        var chatSource = provider.GetRequiredService<IChatSource>();
        var streamTarget = provider.GetRequiredService<IStreamTarget>();
        var eventSource = provider.GetRequiredService<IEventSource>();
        var platformEventSource = provider.GetRequiredService<IPlatformEventSource>();
        var integrationMonitor = provider.GetRequiredService<IIntegrationConnectionMonitor>();
        var options = provider.GetRequiredService<IOptions<TikTokOptions>>().Value;

        Assert.Same(tikTokConnection, platformConnection);
        Assert.Same(tikTokConnection, chatSource);
        Assert.Same(tikTokConnection, streamTarget);
        Assert.Same(tikTokConnection, eventSource);
        Assert.Same(tikTokConnection, platformEventSource);
        Assert.NotSame(tikTokConnection, integrationMonitor);
        Assert.False(options.IsEnabled);
        Assert.Equal("test-creator-123", options.CreatorId);
    }

    [Fact]
    public async Task WhenTikTokIsDisabled_ThenOperationsAreLoggedWithoutEmittingTraffic()
    {
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new TestLoggerProvider(logEntries));
        });

        var connection = new TikTokService(
            Options.Create(new TikTokOptions
            {
                IsEnabled = false,
                CreatorId = "test-creator-123",
                AccessToken = ""
            }),
            loggerFactory.CreateLogger<TikTokService>());

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

        Assert.Empty(authorizationUrl);
        Assert.False(connection.Connected);
        Assert.False(connection.IsConnected);
        Assert.Equal(0, platformEvents);
        Assert.Equal(0, chatEvents);
        Assert.Equal(0, connectionChanges);
        Assert.Contains(logEntries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("TikTok Live connection skipped", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("Disconnecting from TikTok Live", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenTikTokIsEnabled_ThenSendingMessageThrowsNotSupportedException()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
        var connection = new TikTokService(
            Options.Create(new TikTokOptions
            {
                IsEnabled = true,
                CreatorId = "test-creator-123",
                AccessToken = "token"
            }),
            loggerFactory.CreateLogger<TikTokService>());

        await Assert.ThrowsAsync<NotSupportedException>(() => connection.SendMessage("hello world"));
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
