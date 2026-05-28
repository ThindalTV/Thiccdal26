namespace Thiccdal.Data.Models;

/// <summary>
/// Represents a persisted channel-points redemption event.
/// </summary>
public sealed class RedeemEvent : PlatformEvent
{
    public string RewardId { get; set; } = string.Empty;

    public string RewardTitle { get; set; } = string.Empty;

    public string? UserInput { get; set; }
}
