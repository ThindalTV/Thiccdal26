using System;
using System.Collections.Generic;
using System.Text;

namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchTokenManager
{
    Task<string> GetToken(CancellationToken cancellationToken = default);
    Task RefreshToken(CancellationToken cancellationToken = default);
    Task StoreToken(string code, CancellationToken cancellationToken = default);
    string GetAuthorizationUrl();
}
