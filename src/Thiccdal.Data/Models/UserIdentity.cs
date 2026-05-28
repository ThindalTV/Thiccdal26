namespace Thiccdal.Data.Models;

public class UserIdentity
{
    public int Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlatformUser> PlatformUsers { get; } = new List<PlatformUser>();
}
