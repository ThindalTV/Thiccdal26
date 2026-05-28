using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.X;

public interface IXService : IPlatformConnection
{
    XConnectionState ConnectionState { get; }
    bool IsStreamLive { get; }

    event EventHandler<XConnectionState>? ConnectionStateChanged;
    event EventHandler<bool>? StreamLiveStateChanged;

    new Task RefreshConnectionState(CancellationToken cancellationToken = default);
    Task RefreshStreamState(CancellationToken cancellationToken = default);
}
