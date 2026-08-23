using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ChatServiceCommandResponseSinkTests
{
    [Fact]
    public async Task WhenOriginPlatformIsConnected_ThenResponseIsSentOnlyToThatPlatformAndChannel()
    {
        RecordingPlatformConnection twitch = new("Twitch", connected: true);
        RecordingPlatformConnection secondary = new("Null", connected: true);
        ChatServiceCommandResponseSink sink = new(
            [twitch, secondary],
            NullLogger<ChatServiceCommandResponseSink>.Instance);

        await sink.SendResponse(
            new CommandContext
            {
                Trigger = "ai-mention",
                UserDisplayName = "Kaylee",
                Platform = "Twitch",
                SourcePlatform = PlatformEventSource.Twitch,
                ChannelId = "target-channel"
            },
            "Hello there");

        Assert.Single(twitch.Messages);
        Assert.Equal(("Hello there", "target-channel"), twitch.Messages[0]);
        Assert.Empty(secondary.Messages);
    }

    [Fact]
    public async Task WhenOriginPlatformIsNotConnected_ThenResponseIsDropped()
    {
        RecordingPlatformConnection twitch = new("Twitch", connected: false);
        ChatServiceCommandResponseSink sink = new(
            [twitch],
            NullLogger<ChatServiceCommandResponseSink>.Instance);

        await sink.SendResponse(
            new CommandContext
            {
                Trigger = "ai-mention",
                UserDisplayName = "Kaylee",
                Platform = "Twitch",
                SourcePlatform = PlatformEventSource.Twitch,
                ChannelId = "target-channel"
            },
            "Hello there");

        Assert.Empty(twitch.Messages);
    }

    private sealed class RecordingPlatformConnection : IPlatformConnection
    {
        public RecordingPlatformConnection(string platformName, bool connected)
        {
            PlatformName = platformName;
            Connected = connected;
        }

        public bool Connected { get; }

        public string PlatformName { get; }

        public PlatformConnectionState State => Connected ? PlatformConnectionState.Connected : PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public List<(string Message, string? ChannelId)> Messages { get; } = [];

        public event EventHandler<ChatEvent>? OnChatMessageReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add((message, null));
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, string? channelId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add((message, channelId));
            return Task.CompletedTask;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetTitle(string title, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetCategory(string category, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetDescription(string description, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task StartStream(string ingestUrl, string streamKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task StopStream(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
