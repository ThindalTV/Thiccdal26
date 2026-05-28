namespace Thiccdal.Infrastructure.Facebook;

public interface IFacebookConnectionMonitor
{
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
