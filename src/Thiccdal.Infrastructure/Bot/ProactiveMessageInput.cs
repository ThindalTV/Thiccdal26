namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Operator-supplied values for creating or updating an autoresponse.
/// </summary>
public sealed record ProactiveMessageInput(string Message, int IntervalSeconds, bool IsEnabled);
