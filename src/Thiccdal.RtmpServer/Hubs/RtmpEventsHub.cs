using Microsoft.AspNetCore.SignalR;

namespace Thiccdal.RtmpServer.Hubs;

/// <summary>
/// SignalR hub through which the RTMP server pushes lifecycle events to connected bot clients.
/// </summary>
public sealed class RtmpEventsHub : Hub
{
}
