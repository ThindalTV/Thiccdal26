namespace Thiccdal.Data.Models;

public sealed class MembershipEvent : PlatformEvent
{
    public string LevelName { get; set; } = string.Empty;

    public int? MonthCount { get; set; }
}
