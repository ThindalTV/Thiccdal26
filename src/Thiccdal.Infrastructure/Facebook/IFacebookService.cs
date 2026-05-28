using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.Facebook;

public interface IFacebookService : IPlatformConnection
{
    FacebookConnectionState ConnectionState { get; }
    bool IsStreamLive { get; }
    string? LiveVideoId { get; }

    event EventHandler<FacebookConnectionState>? ConnectionStateChanged;
    event EventHandler<bool>? StreamLiveStateChanged;

    new Task RefreshConnectionState(CancellationToken cancellationToken = default);
    Task RefreshStreamState(CancellationToken cancellationToken = default);
    Task<string> StartRelay(string title, string description, CancellationToken cancellationToken = default);
    Task StopRelay(CancellationToken cancellationToken = default);
    Task SetTitle(string title, CancellationToken cancellationToken = default);
    Task SetDescription(string description, CancellationToken cancellationToken = default);
    Task SetCategory(string category, CancellationToken cancellationToken = default);
}
