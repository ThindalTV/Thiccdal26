namespace Thiccdal.Infrastructure.Remotes;

public sealed class NullOptions
{
    public const string SectionName = "Null";

    public string PlatformName { get; set; } = "Null";

    public string AuthorizationUrl { get; set; } = string.Empty;

    public string RtmpRelayUrl { get; set; } = string.Empty;
}
