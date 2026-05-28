namespace Thiccdal.Data.Models;

public sealed class ChatterMemoryReset
{
    public long Id { get; set; }

    public string? Source { get; set; }

    public string? Channel { get; set; }

    public string? PlatformUserId { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime ResetAt { get; set; } = DateTime.UtcNow;
}
