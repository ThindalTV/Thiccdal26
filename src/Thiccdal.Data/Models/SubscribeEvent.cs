namespace Thiccdal.Data.Models;

/// <summary>
/// Represents a persisted subscription event.
/// </summary>
public sealed class SubscribeEvent : PlatformEvent
{
    public string Tier { get; set; } = string.Empty;

    public bool IsGift { get; set; }

    public long? GifterPlatformUserId { get; set; }

    public PlatformUser? GifterPlatformUser { get; set; }
}
