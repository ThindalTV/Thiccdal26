namespace Thiccdal.Infrastructure.Bot;

public sealed record ProactiveMessageDefinition(
    long Id,
    string Message,
    int IntervalSeconds,
    bool IsEnabled,
    DateTimeOffset? LastSentAt);
