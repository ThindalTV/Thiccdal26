namespace Thiccdal.Infrastructure.Questions;

public sealed record QueuedQuestion(
    Guid Id,
    string Platform,
    string PlatformColor,
    string Username,
    string Text,
    DateTimeOffset ReceivedAt,
    QuestionState State = QuestionState.Queued,
    bool IsManual = false,
    DateTimeOffset? FeaturedAt = null)
{
    public static QueuedQuestion CreateDetected(
        string platform,
        string username,
        string text,
        string? platformColor = null,
        DateTimeOffset? receivedAt = null)
    {
        return Create(platform, username, text, platformColor, receivedAt, false);
    }

    public static QueuedQuestion CreateManual(
        string text,
        string username = "Operator",
        DateTimeOffset? receivedAt = null)
    {
        return Create("MANUAL", username, text, "default", receivedAt, true);
    }

    private static QueuedQuestion Create(
        string platform,
        string username,
        string text,
        string? platformColor,
        DateTimeOffset? receivedAt,
        bool isManual)
    {
        return new QueuedQuestion(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(platform) ? "UNKNOWN" : platform.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(platformColor) ? GetDefaultColor(platform) : platformColor.Trim(),
            string.IsNullOrWhiteSpace(username) ? "Viewer" : username.Trim(),
            text.Trim(),
            receivedAt ?? DateTimeOffset.UtcNow,
            QuestionState.Queued,
            isManual);
    }

    private static string GetDefaultColor(string? platform) => platform?.Trim().ToUpperInvariant() switch
    {
        "YOUTUBE" => "live",
        "TWITCH" => "pending",
        "KICK" => "connected",
        "MANUAL" => "default",
        _ => "default"
    };
}
