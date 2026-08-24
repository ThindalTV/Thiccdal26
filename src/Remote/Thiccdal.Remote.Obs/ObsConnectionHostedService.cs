using Microsoft.Extensions.Hosting;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.Obs;

/// <summary>
/// Opens the obs-websocket session at startup and closes it on shutdown.
/// </summary>
public sealed class ObsConnectionHostedService : IHostedService
{
    private readonly IObsConnection _obsConnection;

    public ObsConnectionHostedService(IObsConnection obsConnection)
    {
        ArgumentNullException.ThrowIfNull(obsConnection);

        _obsConnection = obsConnection;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Connect returns as soon as the background session loop is running; OBS being closed at
        // startup is normal and must not block or fail host startup.
        return _obsConnection.Connect(CancellationToken.None);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _obsConnection.Disconnect(cancellationToken);
    }
}
