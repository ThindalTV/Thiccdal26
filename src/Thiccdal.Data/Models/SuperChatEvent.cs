namespace Thiccdal.Data.Models;

public sealed class SuperChatEvent : PlatformEvent
{
    public long AmountMicros { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string DisplayString { get; set; } = string.Empty;

    public string? UserComment { get; set; }
}
