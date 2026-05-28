namespace Thiccdal.Data.Models;

public sealed class ProactiveMessage
{
    public long Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public int IntervalSeconds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastSentAt { get; set; }
}
