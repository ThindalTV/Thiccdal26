namespace Thiccdal.Infrastructure.Sponsors;

public sealed record SponsorConfig
{
    public bool HasSponsor { get; init; }
    public int ReadIntervalMinutes { get; init; }
    public string Script { get; init; } = string.Empty;
    public string OverlayImageUrl { get; init; } = string.Empty;
    public string OverlayTitle { get; init; } = string.Empty;
    public string OverlayLinkUrl { get; init; } = string.Empty;
}
