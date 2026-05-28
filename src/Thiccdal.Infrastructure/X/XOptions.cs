namespace Thiccdal.Infrastructure.X;

public class XOptions
{
    public const string SectionName = "X";

    public string BearerToken { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeySecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string AccessTokenSecret { get; set; } = string.Empty;
    public string OAuthBaseAddress { get; set; } = "https://twitter.com/";
    public string ApiBaseAddress { get; set; } = "https://api.twitter.com/";
    public string ApiVersion { get; set; } = "2";
    public int TweetPollingIntervalSeconds { get; set; } = 15;
    public int PollIntervalMs { get; set; } = 16000;
    public int LikesPollIntervalMs { get; set; } = 30000;
    public int ReconnectDelaySeconds { get; set; } = 30;
    public string BroadcastTweetId { get; set; } = string.Empty;
    public string BroadcastTweetTemplate { get; set; } = "Live now! {title}";
    public string AuthorizationUrl { get; set; } = "https://developer.x.com/en/portal/dashboard";
    public string Channel { get; set; } = "x";
}
