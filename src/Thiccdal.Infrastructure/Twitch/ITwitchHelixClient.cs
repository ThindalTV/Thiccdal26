namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchHelixClient
{
    Task<TwitchSendMessageResult> SendChatMessage(
        TwitchChatConnectionProfile profile,
        string message,
        CancellationToken cancellationToken = default);

    Task<TwitchStreamState> GetStreamState(
        TwitchChatConnectionProfile profile,
        CancellationToken cancellationToken = default);

    Task UpdateChannelInfo(
        TwitchChatConnectionProfile profile,
        string? title,
        string? category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TwitchEventSubSubscription>> GetEventSubscriptions(CancellationToken cancellationToken = default);

    Task CreateEventSubscription(
        TwitchEventSubSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<TwitchUser?> GetAuthenticatedUser(CancellationToken cancellationToken = default);
}
