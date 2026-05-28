namespace Thiccdal.Data.Models;

public class TwitchTargetChannelConfiguration
{
    public int Id { get; set; }

    public string TargetChannel { get; set; } = string.Empty;

    public string BroadcasterId { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
