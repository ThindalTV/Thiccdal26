namespace Thiccdal.Infrastructure.Questions;

public sealed record LiveQuestion(
    Guid SourceQuestionId,
    string Platform,
    string PlatformColor,
    string Username,
    string Text,
    DateTimeOffset ReceivedAt,
    DateTimeOffset PromotedAt);