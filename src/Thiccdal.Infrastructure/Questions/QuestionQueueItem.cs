namespace Thiccdal.Infrastructure.Questions;

public sealed record QuestionQueueItem(
    Guid Id,
    string Platform,
    string PlatformColor,
    string Username,
    string Text,
    DateTimeOffset ReceivedAt,
    bool IsManual = false);