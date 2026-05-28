using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.AI;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class ChatBotAiResponderTests
{
    [Fact]
    public async Task WhenMessageMentionsConfiguredBot_ThenAiReplyIsReturned()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("Short reply", "local-model", "stop"));
        StubChatterMemoryService memoryService = new();
        ChatBotAiResponder responder = CreateResponder(client, memoryService);

        string? response = await responder.TryRespond(CreateChatEvent("Hey Thiccdal, what's up?"));

        Assert.Equal("Short reply", response);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("local-model", client.LastRequest?.Model);
        Assert.Contains("You are Thiccdal.", client.LastRequest?.Messages[0].Content, StringComparison.Ordinal);
        Assert.Equal(1, memoryService.CallCount);
    }

    [Fact]
    public async Task WhenMessageDoesNotMentionConfiguredBot_ThenAiIsNotInvoked()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("Should not send", "local-model", "stop"));
        ChatBotAiResponder responder = CreateResponder(client);

        string? response = await responder.TryRespond(CreateChatEvent("How is chat doing?"));

        Assert.Null(response);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task WhenAiRequestTimesOut_ThenNoReplyIsReturned()
    {
        TimeoutChatCompletionClient client = new();
        ChatBotAiResponder responder = CreateResponder(client);

        string? response = await responder.TryRespond(CreateChatEvent("@thiccdal tell us a joke"));

        Assert.Null(response);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task WhenAiRequestFails_ThenNoReplyIsReturned()
    {
        ThrowingChatCompletionClient client = new();
        ChatBotAiResponder responder = CreateResponder(client);

        string? response = await responder.TryRespond(CreateChatEvent("thiccdal, summarize that"));

        Assert.Null(response);
    }

    [Fact]
    public async Task WhenChatterMemoryEnabled_ThenMemoryIsInjectedIntoPrompt()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("Short reply", "local-model", "stop"));
        StubChatterMemoryService memoryService = new(
            new ChatterMemoryContext(
                "Viewer",
                new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                ["likes soulslikes", "recent topics: speedruns"]));
        ChatBotAiResponder responder = CreateResponder(client, memoryService);

        await responder.TryRespond(CreateChatEvent("@Thiccdal remember me?"));

        Assert.Equal(3, client.LastRequest?.Messages.Count);
        Assert.Contains("likes soulslikes", client.LastRequest?.Messages[1].Content, StringComparison.Ordinal);
        Assert.Contains("same platform and channel", client.LastRequest?.Messages[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenChatterMemoryDisabled_ThenMemoryServiceIsNotUsed()
    {
        StubChatCompletionClient client = new(new AiChatCompletionResult("Short reply", "local-model", "stop"));
        StubChatterMemoryService memoryService = new(
            new ChatterMemoryContext(
                "Viewer",
                new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                ["likes soulslikes"]));
        ChatBotAiResponder responder = CreateResponder(client, memoryService, chatterMemoryEnabled: false);

        await responder.TryRespond(CreateChatEvent("@Thiccdal remember me?"));

        Assert.Equal(0, memoryService.CallCount);
        Assert.Equal(2, client.LastRequest?.Messages.Count);
    }

    private static ChatBotAiResponder CreateResponder(
        IChatCompletionClient client,
        IChatterMemoryService? chatterMemoryService = null,
        bool chatterMemoryEnabled = true)
    {
        return new ChatBotAiResponder(
            client,
            chatterMemoryService ?? new StubChatterMemoryService(),
            Options.Create(
                new ChatBotOptions
                {
                    BotName = "Thiccdal",
                    AiResponder = new ChatBotAiResponderOptions
                    {
                        Enabled = true,
                        ChatterMemoryEnabled = chatterMemoryEnabled,
                        Model = "local-model"
                    }
                }),
            NullLogger<ChatBotAiResponder>.Instance);
    }

    private static ChatEvent CreateChatEvent(string content)
    {
        return new ChatEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.ChatMessage,
            PlatformUserId = "viewer-1",
            Author = "Viewer",
            Channel = "thiccdal",
            ExternalId = Guid.NewGuid().ToString("N"),
            Summary = content,
            Content = content,
            OccurredAt = DateTime.UtcNow
        };
    }

    private sealed class StubChatCompletionClient : IChatCompletionClient
    {
        private readonly AiChatCompletionResult _result;

        public StubChatCompletionClient(AiChatCompletionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public AiChatCompletionRequest? LastRequest { get; private set; }

        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class TimeoutChatCompletionClient : IChatCompletionClient
    {
        public int CallCount { get; private set; }

        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromException<AiChatCompletionResult>(new OperationCanceledException("Timed out."));
        }
    }

    private sealed class ThrowingChatCompletionClient : IChatCompletionClient
    {
        public Task<AiChatCompletionResult> CompleteChat(
            AiChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("Endpoint unavailable");
        }
    }

    private sealed class StubChatterMemoryService : IChatterMemoryService
    {
        private readonly ChatterMemoryContext? _memoryContext;

        public StubChatterMemoryService(ChatterMemoryContext? memoryContext = null)
        {
            _memoryContext = memoryContext;
        }

        public int CallCount { get; private set; }

        public Task<ChatterMemoryContext?> GetMemoryContext(
            PlatformEventSource source,
            string channel,
            string platformUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_memoryContext);
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
}
