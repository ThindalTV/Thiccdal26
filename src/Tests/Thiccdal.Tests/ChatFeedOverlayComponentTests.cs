using System.Threading.Channels;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Modules.Overlay.Components;

namespace Thiccdal.Tests;

public sealed class ChatFeedOverlayComponentTests
{
    [Fact]
    public void WhenChatEventHasPreferredAuthor_ThenOverlayRendersCanonicalName()
    {
        using TestContext context = new();
        FakeChatAggregationService chatAggregationService = new();
        context.Services.AddSingleton<IChatAggregationService>(chatAggregationService);
        context.Services.AddSingleton<IOperatorStateService>(new OperatorStateService());

        IRenderedComponent<ChatFeedOverlayComponent> cut = context.RenderComponent<ChatFeedOverlayComponent>();

        chatAggregationService.Publish(
            new ChatEvent
            {
                Source = PlatformEventSource.Twitch,
                Type = PlatformEventType.ChatMessage,
                Author = "KayleeRaw",
                PreferredAuthor = "Kaylee Prime",
                Channel = "thiccdal",
                Summary = "KayleeRaw said hello",
                Content = "hello there"
            });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Kaylee Prime:", cut.Markup);
            Assert.DoesNotContain("KayleeRaw:", cut.Markup);
        });
    }

    private sealed class FakeChatAggregationService : IChatAggregationService
    {
        private readonly Channel<ChatEvent> _channel = Channel.CreateUnbounded<ChatEvent>();

        public IAsyncEnumerable<ChatEvent> Subscribe(CancellationToken cancellationToken = default)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }

        public void Publish(ChatEvent chatEvent)
        {
            _channel.Writer.TryWrite(chatEvent);
        }
    }
}
