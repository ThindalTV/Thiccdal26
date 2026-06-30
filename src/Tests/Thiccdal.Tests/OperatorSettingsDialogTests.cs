using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LinkedIn;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Remotes.Models;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.TikTok;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Modules.Control.Components.Settings;

namespace Thiccdal.Tests;

public sealed class OperatorSettingsDialogTests
{
    [Fact]
    public void WhenViewerIdentitiesTabSelected_ThenManualMergePanelRenders()
    {
        using TestContext context = new();
        context.Services.AddSingleton<IOperatorStateService>(new OperatorStateService());
        context.Services.AddSingleton<IPlatformManualReminderProvider>(new FakePlatformManualReminderProvider());
        context.Services.AddSingleton<IUserIdentityService>(new FakeUserIdentityService());
        context.Services.AddSingleton<IOptions<LinkedInOptions>>(Options.Create(new LinkedInOptions()));
        context.Services.AddSingleton<IOptions<TikTokOptions>>(Options.Create(new TikTokOptions()));
        context.Services.AddSingleton<IRestreamControlClient>(new FakeRestreamControlClient());
        context.Services.AddSingleton<IEmoteRenderingOptions>(new EmoteRenderingOptions(false));

        IRenderedComponent<OperatorSettingsDialog> cut = context.RenderComponent<OperatorSettingsDialog>();

        Assert.Contains("Stream Settings Checklist", cut.Markup);

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Viewer Identities", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Viewer identities", cut.Markup);
            Assert.Contains("Search viewers", cut.Markup);
        });
    }

    [Fact]
    public void WhenRestreamTabSelected_ThenRestreamControlsRender()
    {
        using TestContext context = new();
        context.Services.AddSingleton<IOperatorStateService>(new OperatorStateService());
        context.Services.AddSingleton<IPlatformManualReminderProvider>(new FakePlatformManualReminderProvider());
        context.Services.AddSingleton<IUserIdentityService>(new FakeUserIdentityService());
        context.Services.AddSingleton<IOptions<LinkedInOptions>>(Options.Create(new LinkedInOptions()));
        context.Services.AddSingleton<IOptions<TikTokOptions>>(Options.Create(new TikTokOptions()));
        context.Services.AddSingleton<IRestreamControlClient>(new FakeRestreamControlClient());
        context.Services.AddSingleton<IEmoteRenderingOptions>(new EmoteRenderingOptions(false));

        IRenderedComponent<OperatorSettingsDialog> cut = context.RenderComponent<OperatorSettingsDialog>();

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Restream", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Choose which connected integrations join RTMP fanout", cut.Markup);
            Assert.Contains("RTMP ingest URL", cut.Markup);
            Assert.Contains("BRB slate path", cut.Markup);
            Assert.Contains("Save configuration", cut.Markup);
            Assert.Contains("Start restream", cut.Markup);
            Assert.Contains("Twitch", cut.Markup);
        });
    }

    private sealed class FakePlatformManualReminderProvider : IPlatformManualReminderProvider
    {
        public IReadOnlyList<PlatformManualReminder> GetReminders()
        {
            return
            [
                new PlatformManualReminder
                {
                    Platform = "Twitch",
                    Setting = "Visibility",
                    ReminderText = "Check Twitch visibility."
                }
            ];
        }
    }

    private sealed class FakeUserIdentityService : IUserIdentityService
    {
        public Task<IReadOnlyList<UserIdentitySearchResult>> Search(string query, CancellationToken cancellationToken = default)
        {
            _ = query;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<UserIdentitySearchResult>>(Array.Empty<UserIdentitySearchResult>());
        }

        public Task<UserIdentityMergeResult> Merge(
            UserIdentityMergeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Merge(request.PlatformUserIds, request.CanonicalName, cancellationToken);
        }

        public Task<UserIdentityMergeResult> Merge(
            IReadOnlyList<long> platformUserIds,
            string? canonicalName,
            CancellationToken cancellationToken = default)
        {
            _ = platformUserIds;
            _ = canonicalName;
            _ = cancellationToken;
            return Task.FromResult(new UserIdentityMergeResult(1, "Alice", Array.Empty<long>()));
        }

        public Task Unlink(long platformUserId, CancellationToken cancellationToken = default)
        {
            _ = platformUserId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRestreamControlClient : IRestreamControlClient
    {
        private RestreamControlState _state = new RestreamControlState
        {
            IngestUrl = "rtmp://localhost:1935/live/operator-settings-tests",
            RecordingOutputPath = "C:\\Streams",
            BrbSlatePath = "C:\\Scenes\\brb.mp4",
            EnabledDestinationCount = 1,
            ConnectedDestinationCount = 1,
            ActiveDestinationCount = 1,
            CanStart = true,
            DependencyNote = "Test dependency note.",
            Destinations =
            [
                new RestreamDestinationState
                {
                    PlatformName = "Twitch",
                    IsConnected = true,
                    IsEnabled = true
                }
            ]
        };

        public Task<RestreamControlState> GetState(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(_state);
        }

        public Task<RestreamControlState> UpdateConfiguration(
            RestreamConfigurationUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = _state with
            {
                IngestUrl = request.IngestUrl,
                RecordingOutputPath = request.RecordingOutputPath,
                StartWithHost = request.StartWithHost,
                BrbSlatePath = request.BrbSlatePath,
                IsBrbSlateConfigured = !string.IsNullOrWhiteSpace(request.BrbSlatePath),
                OperatorMessage = "Restream configuration saved."
            };

            return Task.FromResult(_state);
        }

        public Task<RestreamControlState> UpdateDestination(RestreamDestinationUpdateRequest request, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = _state with
            {
                EnabledDestinationCount = request.IsEnabled ? 1 : 0,
                ActiveDestinationCount = request.IsEnabled ? 1 : 0,
                CanStart = request.IsEnabled,
                Destinations =
                [
                    new RestreamDestinationState
                    {
                        PlatformName = request.PlatformName,
                        IsConnected = true,
                        IsEnabled = request.IsEnabled
                    }
                ]
            };

            return Task.FromResult(_state);
        }

        public Task<RestreamControlState> Start(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = _state with
            {
                IsIngestRunning = true,
                IsFanoutRunning = true,
                OperatorMessage = "Restream ingest and fanout are marked as running."
            };

            return Task.FromResult(_state);
        }

        public Task<RestreamControlState> Stop(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _state = _state with
            {
                IsIngestRunning = false,
                IsFanoutRunning = false,
                OperatorMessage = "Restream ingest and fanout are marked as stopped."
            };

            return Task.FromResult(_state);
        }
    }
}
