using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Components.Admin;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Tests;

public sealed class TwitchSetupDialogTests
{
    private static TwitchSetupDialogTestHarness BuildHarness(
        TwitchConnectionState connectionState = TwitchConnectionState.NotAuthorized,
        bool isVisible = true)
    {
        return new TwitchSetupDialogTestHarness(connectionState, isVisible);
    }

    [Fact]
    public void WhenIsVisibleFalse_ThenDialogNotRendered()
    {
        using TwitchSetupDialogTestHarness harness = BuildHarness(isVisible: false);

        IRenderedComponent<TwitchSetupDialog> cut = harness.Render();

        Assert.DoesNotContain("modal-content", cut.Markup);
    }

    [Fact]
    public void WhenIsVisibleTrue_ThenDialogRendered()
    {
        using TwitchSetupDialogTestHarness harness = BuildHarness(isVisible: true);

        IRenderedComponent<TwitchSetupDialog> cut = harness.Render();

        Assert.Contains("Twitch Setup", cut.Markup);
        Assert.Contains("modal-content", cut.Markup);
    }

    [Fact]
    public void WhenCloseButtonClicked_ThenOnCloseInvoked()
    {
        using TwitchSetupDialogTestHarness harness = BuildHarness(isVisible: true);

        bool closeCalled = false;
        IRenderedComponent<TwitchSetupDialog> cut = harness.Render(onClose: () => closeCalled = true);

        cut.Find(".btn-close").Click();

        Assert.True(closeCalled, "OnClose callback should be invoked when close button is clicked");
    }

    [Fact]
    public void WhenNotAuthorized_ThenAuthorizeButtonRendered()
    {
        using TwitchSetupDialogTestHarness harness = BuildHarness(TwitchConnectionState.NotAuthorized);

        IRenderedComponent<TwitchSetupDialog> cut = harness.Render();

        Assert.Contains("Authorize with Twitch", cut.Markup);
    }

    [Fact]
    public void WhenAuthorized_ThenDisconnectButtonRendered()
    {
        using TwitchSetupDialogTestHarness harness = BuildHarness(TwitchConnectionState.Authorized);

        IRenderedComponent<TwitchSetupDialog> cut = harness.Render();

        Assert.DoesNotContain("Authorize with Twitch", cut.Markup);
        Assert.Contains("Disconnect", cut.Markup);
    }

    private sealed class TwitchSetupDialogTestHarness : IDisposable
    {
        private readonly TestContext _context = new TestContext();
        private readonly TwitchConnectionState _connectionState;
        private readonly bool _isVisible;

        public TwitchSetupDialogTestHarness(TwitchConnectionState connectionState, bool isVisible)
        {
            _connectionState = connectionState;
            _isVisible = isVisible;

            _context.Services.AddSingleton<ITwitchService>(new FakeTwitchService(connectionState));
            _context.Services.AddSingleton<ITwitchTokenManager>(new FakeTwitchTokenManager());
            _context.Services.AddSingleton<ITwitchTargetChannelService>(new FakeTwitchTargetChannelService());
            _context.Services.AddSingleton<ILogger<TwitchSetupDialog>>(NullLogger<TwitchSetupDialog>.Instance);
        }

        public IRenderedComponent<TwitchSetupDialog> Render(Action? onClose = null, Action? onRevoked = null)
        {
            return _context.RenderComponent<TwitchSetupDialog>(p => p
                .Add(c => c.IsVisible, _isVisible)
                .Add(c => c.OnClose, onClose ?? (() => { }))
                .Add(c => c.OnRevoked, onRevoked ?? (() => { })));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }

    private sealed class FakeTwitchService : ITwitchService
    {
        public FakeTwitchService(TwitchConnectionState connectionState)
        {
            ConnectionState = connectionState;
        }

        public string PlatformName => "Twitch";
        public TwitchConnectionState ConnectionState { get; }
        public bool IsStreamLive => false;
        public TwitchStreamState StreamState => new TwitchStreamState();
        public PlatformConnectionState State => ConnectionState == TwitchConnectionState.Connected
            ? PlatformConnectionState.Connected
            : PlatformConnectionState.Disconnected;
        public string? LastError => null;
        public bool Connected => ConnectionState == TwitchConnectionState.Connected;

        public event EventHandler<TwitchConnectionState>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? StreamLiveStateChanged
        {
            add { }
            remove { }
        }

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

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task RefreshStreamState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTwitchTokenManager : ITwitchTokenManager
    {
        public Task<string?> GetToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<string?>(null);
        }

        public Task<bool> HasToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(false);
        }

        public Task RefreshToken(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task StoreToken(string code, CancellationToken cancellationToken = default)
        {
            _ = code;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Revoke(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public string GetAuthorizationUrl() => "https://example.test/auth/twitch";

        public bool ValidateAndConsumeState(string state)
        {
            _ = state;
            return false;
        }
    }

    private sealed class FakeTwitchTargetChannelService : ITwitchTargetChannelService
    {
        public event EventHandler<TwitchChatConnectionProfile>? ConnectionProfileChanged
        {
            add { }
            remove { }
        }

        public Task<TwitchChatConnectionProfile> GetConnectionProfile(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(new TwitchChatConnectionProfile
            {
                BotUsername = "testbot",
                TargetChannel = "testchannel"
            });
        }

        public Task<TwitchChatConnectionProfile> UpdateTargetChannel(
            TwitchTargetChannelSettings targetChannel,
            CancellationToken cancellationToken = default)
        {
            _ = targetChannel;
            _ = cancellationToken;
            throw new NotSupportedException();
        }
    }
}
