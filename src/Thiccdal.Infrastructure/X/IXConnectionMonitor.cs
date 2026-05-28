namespace Thiccdal.Infrastructure.X;

public interface IXConnectionMonitor
{
    Task Start(CancellationToken cancellationToken = default);
    Task Stop(CancellationToken cancellationToken = default);
    Task RefreshConnectionState(CancellationToken cancellationToken = default);
}
