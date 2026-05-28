using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Tests;

public sealed class RouteRenderingTests : IClassFixture<ThiccdalApplicationFactory>
{
    private readonly ThiccdalApplicationFactory _applicationFactory;
    private readonly HttpClient _client;

    public RouteRenderingTests(ThiccdalApplicationFactory applicationFactory)
    {
        _applicationFactory = applicationFactory;
        _client = applicationFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task WhenRequestingDashboard_ThenPreLiveModeRendersByDefault()
    {
        HttpResponseMessage response = await _client.GetAsync("/dashboard");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("THICCDAL", content);
        Assert.Contains("Pre-Live", content);
        Assert.Contains("Pre-Live Checklist", content);
        Assert.Contains("Update All Platforms", content);
        Assert.Contains("Overlay Test Area", content);
        Assert.Contains("Go Live", content);
        Assert.Contains("Preview Live Dashboard", content);
        Assert.Contains("items remaining", content);
        Assert.Contains("Stream Info", content);
        Assert.Contains("OBS &amp; Technical", content);
        Assert.Contains("RTMP ingest URL configured in OBS", content);
        Assert.Contains("rtmp://localhost:1935/live", content);
        Assert.Contains("Copy", content);
        Assert.Contains("Recording", content);
        Assert.Contains("Sufficient disk space", content);
        Assert.Contains("Open full checklist", content);
        Assert.DoesNotContain("Question Queue", content);
    }

    [Fact]
    public async Task WhenDashboardRenderedInLiveMode_ThenLivePanelsRemainAvailable()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["dotnet", "blazor"]);
        operatorStateService.BeginLiveSession(new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero));

        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);

        using HttpClient client = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        HttpResponseMessage response = await client.GetAsync("/dashboard");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Live", content);
        Assert.Contains("Question Queue", content);
        Assert.Contains("Lower Third", content);
        Assert.Contains("Live Controls", content);
        Assert.Contains("Manage Commands", content);
        Assert.Contains("Restream", content);
        Assert.Contains("Go Offline", content);
        Assert.DoesNotContain("chat-feed-overlay", content);
        Assert.DoesNotContain("New question ready", content);
        Assert.DoesNotContain("Pre-Live Checklist", content);
    }

    [Fact]
    public async Task WhenDashboardRenderedWithNewQueuedQuestion_ThenQuestionAttentionFlashRenders()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        questionOverlayService.ClearWaitingQuestions();
        questionOverlayService.AddManualQuestion("Can we pin this next?");
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["dotnet", "blazor"]);
        operatorStateService.BeginLiveSession(new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero));

        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient client = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        HttpResponseMessage response = await client.GetAsync("/dashboard");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Question Queue", content);
        Assert.Contains("New question ready", content);
        Assert.DoesNotContain("chat-feed-overlay", content);
    }

    [Fact]
    public async Task WhenRequestingOverlay_ThenOverlayPageRenders()
    {
        HttpResponseMessage response = await _client.GetAsync("/overlay");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("overlay-root", content);
        Assert.Contains("chat-feed-overlay", content);
        Assert.Contains("event-ticker", content);
    }

    [Fact]
    public async Task WhenOverlayRenderedWithLiveQuestion_ThenLowerThirdContentRenders()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        questionOverlayService.ClearWaitingQuestions();
        questionOverlayService.AddManualQuestion("Can we bring this one up next?");
        questionOverlayService.TryPromoteSelectedQuestion();
        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient client = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        HttpResponseMessage response = await client.GetAsync("/overlay");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("overlay-root", content);
        Assert.Contains("lower-third-overlay-host", content);
        Assert.Contains("Can we bring this one up next?", content);
    }

    [Fact]
    public async Task WhenRequestingTwitchConnect_ThenRedirectsToIntegrations()
    {
        HttpResponseMessage response = await _client.GetAsync("/twitch/connect");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Integrations", content);
    }

    [Fact]
    public async Task WhenRequestingPrompter_ThenPrompterPageRenders()
    {
        HttpResponseMessage response = await _client.GetAsync("/prompter");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("prompterBackground", content);
    }

    [Fact]
    public async Task WhenPrompterRenderedWithStagedStreamTitle_ThenSharedTitleRenders()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        operatorStateService.SetStreamInfo("Phase 15 shared title", "Science & Technology", ["blazor"]);

        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient client = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        HttpResponseMessage response = await client.GetAsync("/prompter");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("prompter-heading__title", content);
        Assert.Contains("Phase 15 shared title", content);
    }

    [Fact]
    public async Task WhenPrompterRenderedWithNewQueuedQuestion_ThenQuestionAttentionFlashRenders()
    {
        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        questionOverlayService.ClearWaitingQuestions();
        questionOverlayService.AddManualQuestion("Do we show a flash here too?");
        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient client = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        HttpResponseMessage response = await client.GetAsync("/prompter");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("prompterBackground", content);
        Assert.Contains("New question ready", content);
    }

    [Fact]
    public async Task WhenSharedStreamInfoChanges_ThenDashboardAndPrompterRenderSameUpdatedTitle()
    {
        const string sharedTitle = "Phase 15 synchronized operator title";

        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient dashboardClient = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        using HttpClient prompterClient = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        string initialDashboardContent = await dashboardClient.GetStringAsync("/dashboard");
        string initialPrompterContent = await prompterClient.GetStringAsync("/prompter");

        Assert.DoesNotContain(sharedTitle, initialDashboardContent);
        Assert.DoesNotContain(sharedTitle, initialPrompterContent);

        operatorStateService.SetStreamInfo(sharedTitle, "Science & Technology", ["blazor", "testing"]);

        string updatedDashboardContent = await dashboardClient.GetStringAsync("/dashboard");
        string updatedPrompterContent = await prompterClient.GetStringAsync("/prompter");

        Assert.Contains(sharedTitle, updatedDashboardContent);
        Assert.Contains(sharedTitle, updatedPrompterContent);
    }

    [Fact]
    public async Task WhenSharedQuestionStateChanges_ThenDashboardAndOverlayRenderSameLiveQuestion()
    {
        const string sharedQuestion = "Can both pages see this shared question?";

        using OperatorStateService operatorStateService = new();
        QuestionOverlayService questionOverlayService = new(operatorStateService);
        questionOverlayService.ClearWaitingQuestions();
        operatorStateService.SetStreamInfo("Ship it", "Science & Technology", ["blazor"]);
        operatorStateService.BeginLiveSession(new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero));

        using WebApplicationFactory<Program> liveFactory = CreateLiveFactory(operatorStateService, questionOverlayService);
        using HttpClient dashboardClient = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        using HttpClient overlayClient = liveFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        string initialDashboardContent = await dashboardClient.GetStringAsync("/dashboard");
        string initialOverlayContent = await overlayClient.GetStringAsync("/overlay");

        Assert.DoesNotContain(sharedQuestion, initialDashboardContent);
        Assert.DoesNotContain(sharedQuestion, initialOverlayContent);

        questionOverlayService.AddManualQuestion(sharedQuestion);
        questionOverlayService.TryPromoteSelectedQuestion();

        string updatedDashboardContent = await dashboardClient.GetStringAsync("/dashboard");
        string updatedOverlayContent = await overlayClient.GetStringAsync("/overlay");

        Assert.Contains("Question Queue", updatedDashboardContent);
        Assert.Contains(sharedQuestion, updatedDashboardContent);
        Assert.Contains("NOW SHOWING", updatedDashboardContent);
        Assert.Contains("lower-third-overlay-host", updatedOverlayContent);
        Assert.Contains(sharedQuestion, updatedOverlayContent);
    }

    [Fact]
    public async Task WhenRequestingChatbotPage_ThenResetControlsRender()
    {
        HttpResponseMessage response = await _client.GetAsync("/chatbot");

        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Chatbot", content);
        Assert.Contains("Reset one chatter memory scope", content);
        Assert.Contains("Source chat history and platform events stay intact", content);
    }

    private WebApplicationFactory<Program> CreateLiveFactory(
        OperatorStateService operatorStateService,
        QuestionOverlayService questionOverlayService)
    {
        return _applicationFactory.WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services =>
                {
                    services.RemoveAll<QuestionOverlayService>();
                    services.RemoveAll<IQuestionOverlayService>();
                    services.RemoveAll<IOperatorStateService>();
                    services.RemoveAll<IPreLiveChecklistService>();

                    services.AddSingleton(questionOverlayService);
                    services.AddSingleton<IQuestionOverlayService>(
                        serviceProvider => serviceProvider.GetRequiredService<QuestionOverlayService>());
                    services.AddSingleton<IOperatorStateService>(operatorStateService);
                    services.AddSingleton<IPreLiveChecklistService>(
                        serviceProvider => new PreLiveChecklistService(
                            serviceProvider.GetRequiredService<IOperatorStateService>(),
                            serviceProvider.GetServices<IPlatformConnection>(),
                            serviceProvider.GetRequiredService<IOverlayService>(),
                            serviceProvider.GetRequiredService<IPlatformManualReminderProvider>(),
                            serviceProvider.GetRequiredService<IRecordingStorageProbe>(),
                            serviceProvider.GetRequiredService<IOptions<StreamingOptions>>(),
                            serviceProvider.GetRequiredService<TimeProvider>()));
                    services.AddSingleton<IRecordingStorageProbe>(
                        new StaticRecordingStorageProbe(new RecordingStorageStatus(
                            false,
                            "Set a recording output folder to enable local capture.",
                            false,
                            "Recording drive monitoring starts after a recording folder is configured.")));
                    services.Configure<StreamingOptions>(_ => { });
                }));
    }

    private sealed class StaticRecordingStorageProbe : IRecordingStorageProbe
    {
        private readonly RecordingStorageStatus _status;

        public StaticRecordingStorageProbe(RecordingStorageStatus status)
        {
            _status = status;
        }

        public RecordingStorageStatus GetStatus()
        {
            return _status;
        }
    }
}