using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Remote.Null;

namespace Thiccdal.Tests;

public sealed class RestreamApiTests
{
    [Fact]
    public async Task WhenConfigurationUpdated_ThenRestreamApiReturnsPersistedSettings()
    {
        await using RestreamApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/restream/configuration",
            new RestreamConfigurationUpdateRequest
            {
                IngestUrl = "rtmp://localhost:1935/live/phase8",
                RecordingOutputPath = "C:\\Thiccdal\\Recordings",
                StartWithHost = true,
                BrbSlatePath = "C:\\Thiccdal\\Assets\\brb.mp4"
            });

        response.EnsureSuccessStatusCode();
        RestreamControlState updatedState = Assert.IsType<RestreamControlState>(
            await response.Content.ReadFromJsonAsync<RestreamControlState>());

        Assert.Equal("rtmp://localhost:1935/live/phase8", updatedState.IngestUrl);
        Assert.Equal("C:\\Thiccdal\\Recordings", updatedState.RecordingOutputPath);
        Assert.True(updatedState.StartWithHost);
        Assert.Equal("C:\\Thiccdal\\Assets\\brb.mp4", updatedState.BrbSlatePath);
        Assert.True(updatedState.IsBrbSlateConfigured);

        RestreamControlState refreshedState = Assert.IsType<RestreamControlState>(
            await client.GetFromJsonAsync<RestreamControlState>("/api/restream"));

        Assert.Equal(updatedState.IngestUrl, refreshedState.IngestUrl);
        Assert.Equal(updatedState.RecordingOutputPath, refreshedState.RecordingOutputPath);
        Assert.Equal(updatedState.BrbSlatePath, refreshedState.BrbSlatePath);
    }

    [Fact]
    public async Task WhenDestinationEnabledAndConnected_ThenRestreamApiStartsAndStopsRuntime()
    {
        await using RestreamApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        NullPlatformConnection nullPlatformConnection = factory.Services.GetRequiredService<NullPlatformConnection>();
        await nullPlatformConnection.Connect(CancellationToken.None);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            "/api/restream/destinations/Null",
            new RestreamDestinationUpdateRequest
            {
                PlatformName = "Null",
                IsEnabled = true
            });

        updateResponse.EnsureSuccessStatusCode();
        RestreamControlState enabledState = Assert.IsType<RestreamControlState>(
            await updateResponse.Content.ReadFromJsonAsync<RestreamControlState>());
        Assert.True(enabledState.CanStart);
        Assert.Contains(enabledState.Destinations, static destination => destination is { PlatformName: "Null", IsEnabled: true, IsConnected: true });

        using HttpResponseMessage startResponse = await client.PostAsJsonAsync("/api/restream/start", new object());
        startResponse.EnsureSuccessStatusCode();
        RestreamControlState startedState = Assert.IsType<RestreamControlState>(
            await startResponse.Content.ReadFromJsonAsync<RestreamControlState>());
        Assert.True(startedState.IsIngestRunning);
        Assert.True(startedState.IsFanoutRunning);
        Assert.False(startedState.IsRecording);
        Assert.Null(startedState.LatestRecording);
        Assert.Equal("Restream ingest and fanout are marked as running.", startedState.OperatorMessage);

        using HttpResponseMessage stopResponse = await client.PostAsJsonAsync("/api/restream/stop", new object());
        stopResponse.EnsureSuccessStatusCode();
        RestreamControlState stoppedState = Assert.IsType<RestreamControlState>(
            await stopResponse.Content.ReadFromJsonAsync<RestreamControlState>());
        Assert.False(stoppedState.IsIngestRunning);
        Assert.False(stoppedState.IsFanoutRunning);
        Assert.False(stoppedState.IsRecording);
        Assert.Null(stoppedState.LatestRecording);
        Assert.Equal("Restream ingest and fanout are marked as stopped.", stoppedState.OperatorMessage);
    }

    [Fact]
    public async Task WhenNoConnectedDestinationEnabled_ThenRestreamApiReturnsSafeOperatorMessage()
    {
        await using RestreamApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage startResponse = await client.PostAsJsonAsync("/api/restream/start", new object());
        startResponse.EnsureSuccessStatusCode();
        RestreamControlState state = Assert.IsType<RestreamControlState>(
            await startResponse.Content.ReadFromJsonAsync<RestreamControlState>());

        Assert.False(state.IsIngestRunning);
        Assert.False(state.IsFanoutRunning);
        Assert.Equal("Enable at least one connected destination before starting restreaming.", state.OperatorMessage);
    }

    private sealed class RestreamApplicationFactory : WebApplicationFactory<Program>
    {
        public RestreamApplicationFactory()
        {
            DatabasePath = Path.Combine(AppContext.BaseDirectory, $"thiccdal-restream-tests-{Guid.NewGuid():N}.db");
        }

        public string DatabasePath { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    Dictionary<string, string?> settings = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = $"Data Source={DatabasePath}",
                        ["Twitch:ClientId"] = "restream-test-client-id",
                        ["Twitch:ClientSecret"] = "restream-test-client-secret",
                        ["Twitch:RedirectUri"] = "https://localhost/auth/twitch/callback",
                        ["Null:PlatformName"] = "Null",
                        ["Null:AuthorizationUrl"] = "https://example.test/null",
                        ["Null:RtmpRelayUrl"] = "rtmp://localhost:1936/live/null",
                        ["Streaming:IngestUrl"] = "rtmp://localhost:1935/live/restream-tests",
                        ["Streaming:RecordingOutputPath"] = Path.Combine(AppContext.BaseDirectory, "restream-api-recordings"),
                        ["Streaming:FfmpegExecutablePath"] = "ffmpeg",
                        ["Streaming:BrbSlatePath"] = ""
                    };

                    configurationBuilder.AddInMemoryCollection(settings);
                });

            builder.ConfigureServices(
                (context, services) =>
                {
                    services.RemoveAll<IPlatformConnection>();
                    services.RemoveAll<IChatSource>();
                    services.RemoveAll<IStreamTarget>();
                    services.RemoveAll<IEventSource>();
                    services.RemoveAll<IPlatformEventSource>();
                    services.RemoveAll<IIntegrationConnectionMonitor>();
                    services.RemoveAll<IPlatformManualReminderProvider>();
                    services.RemoveAll<IRecordingProcessRunner>();

                    services.AddNullIntegration(context.Configuration);
                    services.AddSingleton<IRecordingProcessRunner>(new FakeRecordingProcessRunner());
                });
        }
    }

    private sealed class FakeRecordingProcessRunner : IRecordingProcessRunner
    {
        public IRecordingProcess Start(RecordingProcessRequest request, CancellationToken cancellationToken = default)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new FakeRecordingProcess();
        }
    }

    private sealed class FakeRecordingProcess : IRecordingProcess
    {
        public event EventHandler? Exited;

        public bool HasExited { get; private set; }

        public int ExitCode { get; private set; }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasExited = true;
            ExitCode = 0;
            Exited?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
