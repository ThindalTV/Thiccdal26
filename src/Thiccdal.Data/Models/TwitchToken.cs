using System;
using System.Collections.Generic;
using System.Text;

namespace Thiccdal.Data.Models;

public class TwitchToken
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the Twitch login name for the authenticated user.
    /// Populated from Twitch Helix API after OAuth authorization.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Twitch numeric user ID for the authenticated user.
    /// Populated from Twitch Helix API after OAuth authorization.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}
