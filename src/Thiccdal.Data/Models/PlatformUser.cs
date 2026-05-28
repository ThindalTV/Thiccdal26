using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Models;

public class PlatformUser
{
    public long Id { get; set; }

    public int? UserIdentityId { get; set; }

    public PlatformEventSource Source { get; set; }

    public string PlatformUserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    public bool IsFollower { get; set; }

    public bool IsSubscriber { get; set; }

    public int? SubscriptionMonths { get; set; }

    public bool IsModerator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public UserIdentity? UserIdentity { get; set; }

    public ICollection<ChatMessage> ChatMessages { get; } = new List<ChatMessage>();
}
