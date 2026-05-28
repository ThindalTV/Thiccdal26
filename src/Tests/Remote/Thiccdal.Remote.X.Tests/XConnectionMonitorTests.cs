using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.X;
using Thiccdal.Remote.X;

namespace Thiccdal.Remote.X.Tests;

public class XConnectionMonitorTests
{
    [Fact]
    public void WhenGetAuthorizationUrl_ThenConfiguredDeveloperPortalUrlIsReturned()
    {
        XService service = XTestSupport.CreateService();
        XConnectionMonitor monitor = new(
            service,
            Options.Create(new XOptions
            {
                AuthorizationUrl = "https://developer.x.com/en/portal/dashboard"
            }),
            new RecordingLogger<XConnectionMonitor>());

        string authorizationUrl = monitor.GetAuthorizationUrl();

        Assert.Equal("https://developer.x.com/en/portal/dashboard", authorizationUrl);
    }
}
