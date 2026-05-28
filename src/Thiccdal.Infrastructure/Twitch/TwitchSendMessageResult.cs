namespace Thiccdal.Infrastructure.Twitch;

public sealed record TwitchSendMessageResult
{
    public bool IsSuccessful { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string FailureCode { get; init; } = string.Empty;

    public string FailureMessage { get; init; } = string.Empty;
}
