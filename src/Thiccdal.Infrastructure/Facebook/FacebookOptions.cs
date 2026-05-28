namespace Thiccdal.Infrastructure.Facebook;

public class FacebookOptions
{
    public const string SectionName = "Facebook";

    public string PageAccessToken { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string OAuthBaseAddress { get; set; } = "https://www.facebook.com/";
    public string GraphApiBaseAddress { get; set; } = "https://graph.facebook.com/";
    public string GraphApiVersion { get; set; } = "v21.0";
    public string DefaultPrivacy { get; set; } = "EVERYONE";
    public int PollIntervalMs { get; set; } = 5000;
    public int ReconnectDelaySeconds { get; set; } = 30;
}
