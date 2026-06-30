namespace Thiccdal.Infrastructure.Sponsors;

public interface ISponsorshipService
{
    SponsorConfig? Config { get; }
    SponsorReadState ReadState { get; }
    DateTimeOffset? NextReadAt { get; }

    void Configure(SponsorConfig config);
    void StartRead();
    void EndRead();
    void SkipRead();

    event EventHandler? StateChanged;
}
