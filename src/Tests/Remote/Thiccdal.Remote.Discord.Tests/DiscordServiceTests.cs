using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Thiccdal.Infrastructure.Discord;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Discord.Tests;

public sealed class DiscordServiceTests
{
    [Fact]
    public async Task WhenStartRelayWithoutVoiceChannelId_ThenPlatformOperationExceptionIsThrown()
    {
        DiscordService service = CreateService(new DiscordOptions());

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(
            () => service.StartRelay("rtmp://localhost/live", "stream-key"));

        Assert.Contains("voice channel ID is not configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenStartRelayWithInvalidVoiceChannelId_ThenPlatformOperationExceptionIsThrown()
    {
        DiscordService service = CreateService(new DiscordOptions
        {
            VoiceChannelId = "not-a-snowflake"
        });

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(
            () => service.StartRelay("rtmp://localhost/live", "stream-key"));

        Assert.Contains("is invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenStartRelayWithVoiceChannelId_ThenBlockedReasonIsReturned()
    {
        ListLogger<DiscordService> logger = new();
        DiscordService service = CreateService(
            new DiscordOptions
            {
                VoiceChannelId = "123456789012345678"
            },
            logger);

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(
            () => service.StartRelay("rtmp://localhost/live", "stream-key"));

        Assert.Equal(DiscordRelaySupport.BlockedReason, exception.Message);
        Assert.False(service.RelayStatus.IsSupported);
        Assert.True(logger.Contains(LogLevel.Error, "relay cannot start"));
    }

    [Fact]
    public async Task WhenStopRelay_ThenBlockedStateIsLoggedWithoutThrowing()
    {
        ListLogger<DiscordService> logger = new();
        DiscordService service = CreateService(new DiscordOptions(), logger);

        await service.StopRelay();

        Assert.True(logger.Contains(LogLevel.Information, "no relay session can exist"));
    }

    private static DiscordService CreateService(
        DiscordOptions options,
        ListLogger<DiscordService>? logger = null)
    {
        return new DiscordService(
            Options.Create(options),
            Substitute.For<IEventBus>(),
            logger ?? new ListLogger<DiscordService>());
    }
}

internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

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

    public bool Contains(LogLevel logLevel, string messageFragment)
    {
        return _entries.Any(entry =>
            entry.Level == logLevel &&
            entry.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
