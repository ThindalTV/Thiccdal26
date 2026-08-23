using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Infrastructure.Readiness;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Tests;

public sealed class SystemReadinessServiceTests
{
    [Fact]
    public async Task WhenNothingIsConfigured_ThenNeitherSurfaceIsReady()
    {
        using SystemReadinessService service = CreateService(targetChannel: string.Empty, hasToken: false);

        SystemReadiness readiness = await service.GetReadiness();

        Assert.False(readiness.HasChannel);
        Assert.False(readiness.HasTwitchAuth);
        Assert.False(readiness.IsPrompterReady);
        Assert.False(readiness.IsDashboardReady);
    }

    [Fact]
    public async Task WhenOnlyChannelIsSaved_ThenPrompterIsReadyButDashboardIsNot()
    {
        using SystemReadinessService service = CreateService(targetChannel: "thindaltv", hasToken: false);

        SystemReadiness readiness = await service.GetReadiness();

        Assert.True(readiness.IsPrompterReady);
        Assert.False(readiness.IsDashboardReady);
    }

    [Fact]
    public async Task WhenChannelAndAuthExist_ThenBothSurfacesAreReady()
    {
        using SystemReadinessService service = CreateService(targetChannel: "thindaltv", hasToken: true);

        SystemReadiness readiness = await service.GetReadiness();

        Assert.True(readiness.IsPrompterReady);
        Assert.True(readiness.IsDashboardReady);
    }

    [Fact]
    public async Task WhenOnlyAuthExists_ThenDashboardStaysBlocked()
    {
        using SystemReadinessService service = CreateService(targetChannel: string.Empty, hasToken: true);

        SystemReadiness readiness = await service.GetReadiness();

        Assert.True(readiness.HasTwitchAuth);
        Assert.False(readiness.IsDashboardReady);
    }

    [Fact]
    public async Task WhenTheTargetChannelServiceThrows_ThenReadinessReportsUnconfiguredInsteadOfFailing()
    {
        FakeTargetChannelService targetChannelService = new(string.Empty) { ShouldThrow = true };
        using SystemReadinessService service = new(
            targetChannelService,
            new FakeTokenManager(hasToken: true),
            NullLogger<SystemReadinessService>.Instance);

        SystemReadiness readiness = await service.GetReadiness();

        Assert.False(readiness.HasChannel);
        Assert.True(readiness.HasTwitchAuth);
    }

    [Fact]
    public void WhenTheTargetChannelChanges_ThenReadinessChangedIsRaised()
    {
        FakeTargetChannelService targetChannelService = new(string.Empty);
        using SystemReadinessService service = new(
            targetChannelService,
            new FakeTokenManager(hasToken: false),
            NullLogger<SystemReadinessService>.Instance);
        int changedCount = 0;

        service.ReadinessChanged += (_, _) => changedCount++;
        targetChannelService.RaiseProfileChanged("thindaltv");

        Assert.Equal(1, changedCount);
    }

    private static SystemReadinessService CreateService(string targetChannel, bool hasToken)
    {
        return new SystemReadinessService(
            new FakeTargetChannelService(targetChannel),
            new FakeTokenManager(hasToken),
            NullLogger<SystemReadinessService>.Instance);
    }

    private sealed class FakeTargetChannelService : ITwitchTargetChannelService
    {
        private string _targetChannel;

        public FakeTargetChannelService(string targetChannel)
        {
            _targetChannel = targetChannel;
        }

        public bool ShouldThrow { get; set; }

        public event EventHandler<TwitchChatConnectionProfile>? ConnectionProfileChanged;

        public Task<TwitchChatConnectionProfile> GetConnectionProfile(CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Settings store unavailable.");
            }

            return Task.FromResult(BuildProfile(_targetChannel));
        }

        public Task<TwitchChatConnectionProfile> UpdateTargetChannel(
            TwitchTargetChannelSettings targetChannel,
            CancellationToken cancellationToken = default)
        {
            _targetChannel = targetChannel.TargetChannel;
            return Task.FromResult(BuildProfile(_targetChannel));
        }

        public void RaiseProfileChanged(string targetChannel)
        {
            _targetChannel = targetChannel;
            ConnectionProfileChanged?.Invoke(this, BuildProfile(targetChannel));
        }

        private static TwitchChatConnectionProfile BuildProfile(string targetChannel)
        {
            return new TwitchChatConnectionProfile
            {
                BotUsername = "thiccdal",
                TargetChannel = targetChannel
            };
        }
    }

    private sealed class FakeTokenManager : ITwitchTokenManager
    {
        private readonly bool _hasToken;

        public FakeTokenManager(bool hasToken)
        {
            _hasToken = hasToken;
        }

        public Task<string?> GetToken(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(_hasToken ? "token" : null);

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(_hasToken);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => string.Empty;

        public bool ValidateAndConsumeState(string state) => true;
    }
}
