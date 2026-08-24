using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.AI;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Teleprompter;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public async Task WhenMessageHasMatchingTrigger_ThenResponseIsSentToChat()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello {user}!")],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!hello", "Kaylee"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Hello Kaylee!", responseSink.Messages[0]);
    }

    [Fact]
    public async Task WhenMessageHasNoTrigger_ThenNoResponseIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello!")],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("hello there"));

        Assert.Empty(responseSink.Messages);
    }

    [Fact]
    public async Task WhenMessageMentionsConfiguredBotAndNoCommandMatches_ThenAiResponseIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        StubChatBotAiResponder aiResponder = new("Hey there!");
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello!")],
            responseSink: responseSink,
            aiResponder: aiResponder);

        await dispatcher.Dispatch(CreateChatEvent("thiccdal, how are you?"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Hey there!", responseSink.Messages[0]);
        Assert.Equal(1, aiResponder.CallCount);
    }

    [Fact]
    public async Task WhenTriggerNotInRegistry_ThenNoResponseIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello!")],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!unknown"));

        Assert.Empty(responseSink.Messages);
    }

    [Fact]
    public async Task WhenMessageDoesNotMentionConfiguredBot_ThenNoResponseIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        StubChatCompletionClient aiClient = new(new AiChatCompletionResult("Should not send", "local-model", "stop"));
        ChatBotAiResponder aiResponder = CreateAiResponder(aiClient);
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello!")],
            responseSink: responseSink,
            aiResponder: aiResponder);

        await dispatcher.Dispatch(CreateChatEvent("hello there"));

        Assert.Empty(responseSink.Messages);
        Assert.Equal(0, aiClient.CallCount);
    }

    [Fact]
    public async Task WhenTriggerMatchesCaseInsensitively_ThenCommandIsDispatched()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello {user}!")],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!HeLLo", "Inara"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Hello Inara!", responseSink.Messages[0]);
    }

    [Fact]
    public async Task WhenCommandIsDisabled_ThenNoResponseIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello!", isEnabled: false)],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!hello"));

        Assert.Empty(responseSink.Messages);
    }

    [Fact]
    public async Task WhenNoHandlerType_ThenStaticResponseTemplateIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!socials", "Follow {platform} {user}")],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!socials", "River", PlatformEventSource.Null));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Follow Null River", responseSink.Messages[0]);
    }

    [Fact]
    public async Task WhenHandlerReturnsString_ThenHandlerResponseIsSentInsteadOfTemplate()
    {
        RecordingCommandResponseSink responseSink = new();
        OverrideHandler handler = new("Handled by code");
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Static response", typeof(OverrideHandler).FullName)],
            handlers: [handler],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!hello there"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Handled by code", responseSink.Messages[0]);
    }

    [Fact]
    public async Task WhenExplicitCommandMatches_ThenAiResponderIsNotInvoked()
    {
        RecordingCommandResponseSink responseSink = new();
        StubChatBotAiResponder aiResponder = new("Should not send");
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Hello {user}!")],
            responseSink: responseSink,
            aiResponder: aiResponder);

        await dispatcher.Dispatch(CreateChatEvent("!hello thiccdal"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Hello Mal!", responseSink.Messages[0]);
        Assert.Equal(0, aiResponder.CallCount);
    }

    [Fact]
    public async Task WhenHandlerReturnsNull_ThenStaticTemplateIsSuppressed()
    {
        RecordingCommandResponseSink responseSink = new();
        OverrideHandler handler = new(null);
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Static response", typeof(OverrideHandler).FullName)],
            handlers: [handler],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!hello"));

        Assert.Empty(responseSink.Messages);
    }

    [Fact]
    public async Task WhenHandlerTypeCannotBeResolved_ThenWarningIsLoggedAndStaticTemplateIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        RecordingLogger<CommandDispatcher> logger = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Fallback response", "Thiccdal.Tests.DoesNotExistHandler")],
            responseSink: responseSink,
            logger: logger);

        await dispatcher.Dispatch(CreateChatEvent("!hello"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Fallback response", responseSink.Messages[0]);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Warning &&
                     entry.Message.Contains("Could not resolve command handler type", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenHandlerIsNotRegistered_ThenWarningIsLoggedAndStaticTemplateIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        RecordingLogger<CommandDispatcher> logger = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Fallback response", typeof(OverrideHandler).FullName)],
            responseSink: responseSink,
            logger: logger);

        await dispatcher.Dispatch(CreateChatEvent("!hello"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Fallback response", responseSink.Messages[0]);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Warning &&
                     entry.Message.Contains("is not registered in DI", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenHandlerThrows_ThenErrorIsLoggedAndStaticTemplateIsSent()
    {
        RecordingCommandResponseSink responseSink = new();
        RecordingLogger<CommandDispatcher> logger = new();
        ThrowingHandler handler = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!hello", "Fallback response", typeof(ThrowingHandler).FullName)],
            handlers: [handler],
            responseSink: responseSink,
            logger: logger);

        await dispatcher.Dispatch(CreateChatEvent("!hello"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Fallback response", responseSink.Messages[0]);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Error &&
                     entry.Message.Contains("threw while dispatching", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenCommandIsDispatched_ThenUseCountIsIncrementedForTheSession()
    {
        RecordingCommandResponseSink responseSink = new();
        RecordingCommandUsageTracker usageTracker = new();
        StubBotCommandManagementService managementService = new([]);
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!count", "Use {count}")],
            usageTracker: usageTracker,
            responseSink: responseSink,
            managementService: managementService);

        await dispatcher.Dispatch(CreateChatEvent("!count"));
        await dispatcher.Dispatch(CreateChatEvent("!count"));

        Assert.Equal(["Use 1", "Use 2"], responseSink.Messages);
        Assert.Equal(2, usageTracker.RecordedTriggers.Count);
        Assert.All(usageTracker.RecordedTriggers, recordedTrigger => Assert.Equal("!count", recordedTrigger));
    }

    [Fact]
    public async Task WhenCommandsMetaCommandIsInvoked_ThenEnabledTriggersAreListed()
    {
        RecordingCommandResponseSink responseSink = new();
        CommandDispatcher dispatcher = CreateDispatcher(
            [
                CreateCommand("!hello", "Hello!"),
                CreateCommand("!socials", "Links!"),
                CreateCommand("!disabled", "Nope", isEnabled: false)
            ],
            responseSink: responseSink);

        await dispatcher.Dispatch(CreateChatEvent("!commands"));

        Assert.Single(responseSink.Messages);
        Assert.Equal("Available commands: !commands, !hello, !socials", responseSink.Messages[0]);
    }

    [Fact]
    public async Task WhenOperatorRunsSavedCommand_ThenResponseIsBroadcastToConnectedPlatforms()
    {
        RecordingPlatformConnection twitchConnection = new("Twitch");
        RecordingPlatformConnection secondaryConnection = new("Null");
        RecordingCommandUsageTracker usageTracker = new();
        StubBotCommandManagementService managementService = new([CreateCommand("!clip", "Clip {count}")]);
        CommandDispatcher dispatcher = CreateDispatcher(
            [CreateCommand("!clip", "Clip {count}")],
            usageTracker: usageTracker,
            managementService: managementService,
            platformConnections: [twitchConnection, secondaryConnection]);

        await dispatcher.DispatchFromOperator("clip");

        Assert.Equal(["Clip 1"], twitchConnection.SentMessages);
        Assert.Equal(["Clip 1"], secondaryConnection.SentMessages);
        Assert.Equal(["!clip"], usageTracker.RecordedTriggers);
        Assert.Equal(["!clip"], managementService.IncrementedTriggers);
    }

    [Fact]
    public async Task WhenOperatorRunsLowerThirdCommand_ThenCopyGoesToTheOverlay()
    {
        RecordingLowerThirdService lowerThirdService = new();
        BotCommandDefinition command = CreateCommand("!discord", "Join the Discord!");
        command.ShowOnLowerThird = true;
        command.LowerThirdTitle = "DISCORD";
        command.LowerThirdText = "discord.gg/thiccdal";
        CommandDispatcher dispatcher = CreateDispatcher(
            [command],
            platformConnections: [new RecordingPlatformConnection("Twitch")],
            lowerThirdService: lowerThirdService);

        await dispatcher.DispatchFromOperator("discord");

        LowerThirdContent shownContent = Assert.Single(lowerThirdService.ShownMessages);
        Assert.Equal("DISCORD", shownContent.Eyebrow);
        Assert.Equal("discord.gg/thiccdal", shownContent.Text);
    }

    [Fact]
    public async Task WhenLowerThirdCommandHasNoOwnCopy_ThenTheChatResponseIsShown()
    {
        RecordingLowerThirdService lowerThirdService = new();
        BotCommandDefinition command = CreateCommand("!discord", "Join the Discord!");
        command.ShowOnLowerThird = true;
        CommandDispatcher dispatcher = CreateDispatcher(
            [command],
            platformConnections: [new RecordingPlatformConnection("Twitch")],
            lowerThirdService: lowerThirdService);

        await dispatcher.DispatchFromOperator("discord");

        LowerThirdContent shownContent = Assert.Single(lowerThirdService.ShownMessages);
        Assert.Equal("!discord", shownContent.Eyebrow);
        Assert.Equal("Join the Discord!", shownContent.Text);
    }

    [Fact]
    public async Task WhenCommandDoesNotSendInChat_ThenOperatorRunSkipsThePlatforms()
    {
        RecordingPlatformConnection twitchConnection = new("Twitch");
        RecordingLowerThirdService lowerThirdService = new();
        BotCommandDefinition command = CreateCommand("!brb", "Be right back");
        command.SendInChat = false;
        command.ShowOnLowerThird = true;
        CommandDispatcher dispatcher = CreateDispatcher(
            [command],
            platformConnections: [twitchConnection],
            lowerThirdService: lowerThirdService);

        await dispatcher.DispatchFromOperator("brb");

        Assert.Empty(twitchConnection.SentMessages);
        Assert.Single(lowerThirdService.ShownMessages);
    }

    [Fact]
    public async Task WhenViewerRunsLowerThirdCommand_ThenTheOverlayIsUntouched()
    {
        RecordingCommandResponseSink responseSink = new();
        RecordingLowerThirdService lowerThirdService = new();
        BotCommandDefinition command = CreateCommand("!discord", "Join the Discord!");
        command.ShowOnLowerThird = true;
        CommandDispatcher dispatcher = CreateDispatcher(
            [command],
            responseSink: responseSink,
            lowerThirdService: lowerThirdService);

        await dispatcher.Dispatch(CreateChatEvent("!discord"));

        Assert.Empty(lowerThirdService.ShownMessages);
        Assert.Single(responseSink.Messages);
    }

    private static CommandDispatcher CreateDispatcher(
        IReadOnlyList<BotCommandDefinition> commands,
        IReadOnlyList<ICommandHandler>? handlers = null,
        RecordingCommandUsageTracker? usageTracker = null,
        RecordingCommandResponseSink? responseSink = null,
        RecordingLogger<CommandDispatcher>? logger = null,
        StubBotCommandManagementService? managementService = null,
        IChatBotAiResponder? aiResponder = null,
        IReadOnlyList<IPlatformConnection>? platformConnections = null,
        RecordingLowerThirdService? lowerThirdService = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IOperatorStateService>(new StubOperatorStateService(
            new OperatorStreamState
            {
                Title = "Live",
                Category = "Gaming",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-90),
                Tags = []
            }));
        services.AddSingleton<IBotCommandManagementService>(managementService ?? new StubBotCommandManagementService(commands));

        if (handlers is not null)
        {
            foreach (ICommandHandler handler in handlers)
            {
                services.AddSingleton(handler.GetType(), handler);
            }
        }

        ServiceProvider provider = services.BuildServiceProvider();

        return new CommandDispatcher(
            new StubCommandRegistry(commands),
            responseSink ?? new RecordingCommandResponseSink(),
            usageTracker ?? new RecordingCommandUsageTracker(),
            new TokenInterpolator(new FixedTimeProvider(DateTimeOffset.UtcNow)),
            provider.GetRequiredService<IOperatorStateService>(),
            aiResponder ?? new StubChatBotAiResponder(null),
            lowerThirdService ?? new RecordingLowerThirdService(),
            provider,
            platformConnections ?? Array.Empty<IPlatformConnection>(),
            logger ?? new RecordingLogger<CommandDispatcher>());
    }

    private static ChatBotAiResponder CreateAiResponder(StubChatCompletionClient client)
    {
        return new ChatBotAiResponder(
            client,
            new StubChatterMemoryService(),
            Options.Create(
                new ChatBotOptions
                {
                    BotName = "Thiccdal",
                    AiResponder = new ChatBotAiResponderOptions
                    {
                        Enabled = true,
                        Model = "local-model"
                    }
                }),
            NullLogger<ChatBotAiResponder>.Instance);
    }

    private sealed class RecordingLowerThirdService : ILowerThirdService
    {
        private LowerThirdContent? _current;

        public event EventHandler? StateChanged;

        public List<LowerThirdContent> ShownMessages { get; } = [];

        public int ClearCount { get; private set; }

        public LowerThirdContent? GetCurrent()
        {
            return _current;
        }

        public void ShowMessage(string eyebrow, string text, string? accent = null)
        {
            _current = new LowerThirdContent(
                LowerThirdContentKind.Message,
                eyebrow,
                text,
                accent ?? "default",
                DateTimeOffset.UnixEpoch,
                null);
            ShownMessages.Add(_current);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _current = null;
            ClearCount++;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static BotCommandDefinition CreateCommand(
        string trigger,
        string responseTemplate,
        string? handlerType = null,
        bool isEnabled = true,
        int useCount = 0,
        long id = 1)
    {
        return new BotCommandDefinition
        {
            Id = id,
            Trigger = trigger,
            ResponseTemplate = responseTemplate,
            HandlerType = handlerType,
            IsEnabled = isEnabled,
            UseCount = useCount
        };
    }

    private static ChatEvent CreateChatEvent(
        string content,
        string author = "Mal",
        PlatformEventSource source = PlatformEventSource.Twitch)
    {
        return new ChatEvent
        {
            Source = source,
            Type = PlatformEventType.ChatMessage,
            PlatformUserId = $"{author.ToLowerInvariant()}-id",
            Author = author,
            Channel = "thiccdal",
            ExternalId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            Summary = content,
            Content = content,
            OccurredAt = DateTime.UtcNow
        };
    }

    private sealed class StubCommandRegistry : ICommandRegistry
    {
        private readonly IReadOnlyList<BotCommandDefinition> _commands;

        public StubCommandRegistry(IReadOnlyList<BotCommandDefinition> commands)
        {
            _commands = commands;
        }

        public IReadOnlyList<BotCommandDefinition> GetEnabledCommands()
        {
            return _commands.Where(command => command.IsEnabled).ToArray();
        }

        public Task Reload(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class StubBotCommandManagementService : IBotCommandManagementService
    {
        private readonly IReadOnlyList<BotCommandDefinition> _commands;

        public StubBotCommandManagementService(IReadOnlyList<BotCommandDefinition> commands)
        {
            _commands = commands;
        }

        public List<string> IncrementedTriggers { get; } = [];

        public Task<IReadOnlyList<BotCommandDefinition>> List(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_commands);
        }

        public Task<BotCommandDefinition> Create(BotCommandDefinitionInput command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BotCommandDefinition?> Update(long id, BotCommandDefinitionInput command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Delete(long id, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task IncrementUseCount(string trigger, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IncrementedTriggers.Add(trigger);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCommandUsageTracker : ICommandUsageTracker
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RecordedTriggers { get; } = [];

        public Task<int> RecordUse(string trigger, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RecordedTriggers.Add(trigger);
            int currentCount = _counts.GetValueOrDefault(trigger, 0) + 1;
            _counts[trigger] = currentCount;
            return Task.FromResult(currentCount);
        }
    }

    private sealed class RecordingCommandResponseSink : ICommandResponseSink
    {
        public List<string> Messages { get; } = [];

        public Task SendResponse(CommandContext context, string response, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(response);
            return Task.CompletedTask;
        }
    }

    private sealed class OverrideHandler : ICommandHandler
    {
        private readonly string? _response;

        public OverrideHandler(string? response)
        {
            _response = response;
        }

        public Task<string?> Handle(CommandContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_response);
        }
    }

    private sealed class StubChatBotAiResponder : IChatBotAiResponder
    {
        private readonly string? _response;

        public StubChatBotAiResponder(string? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public Task<string?> TryRespond(ChatEvent chatEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_response);
        }
    }

    private sealed class StubChatCompletionClient : IChatCompletionClient
    {
        private readonly AiChatCompletionResult _result;

        public StubChatCompletionClient(AiChatCompletionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingHandler : ICommandHandler
    {
        public Task<string?> Handle(CommandContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Boom");
        }
    }

    private sealed class StubChatterMemoryService : IChatterMemoryService
    {
        public Task<ChatterMemoryContext?> GetMemoryContext(
            PlatformEventSource source,
            string channel,
            string platformUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ChatterMemoryContext?>(null);
        }

        public Task Reset(
            PlatformEventSource source,
            string channel,
            string platformUserId,
            string requestedBy,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ResetAll(string requestedBy, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubOperatorStateService : IOperatorStateService
    {
        private readonly OperatorStreamState? _streamState;
        private readonly IReadOnlyList<string> _streamTags;

        public StubOperatorStateService(OperatorStreamState? streamState)
        {
            _streamState = streamState;
            _streamTags = streamState?.Tags ?? [];
        }

        public event EventHandler? StateChanged
#pragma warning disable CS0067
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? OverlayTestTriggered
        {
            add { }
            remove { }
        }
#pragma warning restore CS0067

        public OperatorMode Mode => _streamState is null ? OperatorMode.PreLive : OperatorMode.Live;

        public string StreamTitle => _streamState?.Title ?? string.Empty;

        public string StreamCategory => _streamState?.Category ?? string.Empty;

        public IReadOnlyList<string> StreamTags => _streamTags;

        public DateTimeOffset? LiveStartedAt => _streamState?.StartedAt;

        public int TeleprompterScrollPosition => 0;

        public IReadOnlyList<QueuedQuestion> QuestionQueue => [];

        public QuestionDashboardState GetQuestionState()
        {
            return QuestionDashboardState.Empty;
        }

        public OperatorStreamState? GetActiveStreamState()
        {
            return _streamState;
        }

        public void TriggerOverlayTest(string componentName)
        {
            _ = componentName;
        }

        public void ScrollTeleprompter(ScrollDirection direction)
        {
            _ = direction;
        }

        public void AddQuestion(QueuedQuestion question)
        {
            _ = question;
        }

        public void DismissQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void FeatureQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void CompleteQuestion(Guid questionId)
        {
            _ = questionId;
        }

        public void SetMode(OperatorMode mode)
        {
            _ = mode;
        }

        public void SetStreamInfo(string title, string category, IReadOnlyList<string> tags)
        {
            _ = title;
            _ = category;
            _ = tags;
        }

        public void BeginLiveSession(DateTimeOffset? startedAt = null, Guid? sessionId = null)
        {
            _ = sessionId;
        }

        public bool IsManualReminderReviewed(string platform, string setting)
        {
            _ = platform;
            _ = setting;
            return false;
        }

        public void SetManualReminderReviewed(string platform, string setting, bool isReviewed)
        {
            _ = platform;
            _ = setting;
            _ = isReviewed;
        }

        public bool ClearManualReminderReviews()
        {
            return false;
        }

        public void SetActiveStreamState(OperatorStreamState? streamState)
        {
            _ = streamState;
        }

        public bool AreAllManualRemindersReviewed(IEnumerable<PlatformManualReminder> reminders)
        {
            _ = reminders;
            return false;
        }
    }

    private sealed class RecordingPlatformConnection : IPlatformConnection
    {
        public RecordingPlatformConnection(string platformName)
        {
            PlatformName = platformName;
        }

        public string PlatformName { get; }

        public PlatformConnectionState State => Connected
            ? PlatformConnectionState.Connected
            : PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool Connected { get; set; } = true;

        public List<string> SentMessages { get; } = [];

        public event EventHandler<ChatEvent>? OnChatMessageReceived
#pragma warning disable CS0067
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }
#pragma warning restore CS0067

        public Task Connect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = false;
            return Task.CompletedTask;
        }

        public Task SendMessage(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
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
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);
}
