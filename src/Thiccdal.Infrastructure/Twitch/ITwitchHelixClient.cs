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

    Task DeleteEventSubscription(string subscriptionId, CancellationToken cancellationToken = default);

    Task<TwitchUser?> GetAuthenticatedUser(CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a Twitch user by login name, so a channel name can be turned into the numeric id EventSub requires.
    /// </summary>
    /// <param name="login">The Twitch login name to look up.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<TwitchUser?> GetUserByLogin(string login, CancellationToken cancellationToken = default);
}
