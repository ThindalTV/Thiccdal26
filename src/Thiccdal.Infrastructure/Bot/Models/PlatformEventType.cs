namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Identifies the normalized type of a platform event.
/// </summary>
public enum PlatformEventType
{
    ChatMessage = 1,
    Follow = 2,
    Subscribe = 3,
    Cheer = 4,
    Raid = 5,
    Redeem = 6,
    SuperChat = 7,
    Membership = 8,
    Raw = 99
}
