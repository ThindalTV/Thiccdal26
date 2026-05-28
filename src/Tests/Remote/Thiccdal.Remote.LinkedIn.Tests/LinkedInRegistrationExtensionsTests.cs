using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.LinkedIn;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Remote.LinkedIn;

namespace Thiccdal.Remote.LinkedIn.Tests;

public class LinkedInRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingLinkedInIntegration_ThenRegistersSharedLinkedInServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{LinkedInOptions.SectionName}:IsEnabled"] = "false",
                [$"{LinkedInOptions.SectionName}:OrganizationId"] = "test-org-123",
                [$"{LinkedInOptions.SectionName}:AccessToken"] = ""
            })
            .Build();

        services.AddLinkedInIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        var linkedinConnection = provider.GetRequiredService<LinkedInService>();
        var platformConnection = provider.GetRequiredService<IPlatformConnection>();
        var chatSource = provider.GetRequiredService<IChatSource>();
        var streamTarget = provider.GetRequiredService<IStreamTarget>();
        var eventSource = provider.GetRequiredService<IEventSource>();
        var platformEventSource = provider.GetRequiredService<IPlatformEventSource>();
        var integrationMonitor = provider.GetRequiredService<IIntegrationConnectionMonitor>();
        var options = provider.GetRequiredService<IOptions<LinkedInOptions>>().Value;

        Assert.Same(linkedinConnection, platformConnection);
        Assert.Same(linkedinConnection, chatSource);
        Assert.Same(linkedinConnection, streamTarget);
        Assert.Same(linkedinConnection, eventSource);
        Assert.Same(linkedinConnection, platformEventSource);
        Assert.NotSame(linkedinConnection, integrationMonitor);
        Assert.False(options.IsEnabled);
        Assert.Equal("test-org-123", options.OrganizationId);
    }

    [Fact]
    public async Task WhenLinkedInIsDisabled_ThenOperationsAreLoggedWithoutEmittingTraffic()
    {
        var logEntries = new List<LogEntry>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new TestLoggerProvider(logEntries));
        });

        var connection = new LinkedInService(
            Options.Create(new LinkedInOptions
            {
                IsEnabled = false,
                OrganizationId = "test-org-123",
                AccessToken = ""
            }),
            loggerFactory.CreateLogger<LinkedInService>());

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
            entry.Message.Contains("LinkedIn Live connection skipped", StringComparison.Ordinal));
        Assert.Contains(logEntries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("Disconnecting from LinkedIn Live", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenLinkedInIsEnabled_ThenSendingMessageThrowsNotSupportedException()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.ClearProviders());
        var connection = new LinkedInService(
            Options.Create(new LinkedInOptions
            {
                IsEnabled = true,
                OrganizationId = "test-org-123",
                AccessToken = "token"
            }),
            loggerFactory.CreateLogger<LinkedInService>());

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
