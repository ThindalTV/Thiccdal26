using System;
using System.Collections.Generic;
using System.Text;

namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchService
{
    public event EventHandler<string>? OnMessageRecieved;

    public Task Connect(CancellationToken cancellationToken = default);
    public Task Disconnect(CancellationToken cancellationToken = default);
}
