using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Remote.Obs;

namespace Thiccdal.Remote.Obs.Tests;

public sealed class ObsWebSocketConnectionTests
{
    [Fact]
    public async Task WhenIntegrationIsDisabled_ThenConnectLeavesTheConnectionIdle()
    {
        await using ObsWebSocketConnection connection = CreateConnection(new ObsOptions { Enabled = false });

        await connection.Connect(CancellationToken.None);

        ObsState state = connection.GetState();

        Assert.False(state.IsEnabled);
        Assert.False(state.IsConnected);
        Assert.False(state.IsStreaming);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task WhenIntegrationIsEnabled_ThenInitialStateReportsEnabledButDisconnected()
    {
        await using ObsWebSocketConnection connection = CreateConnection(new ObsOptions { Enabled = true });

        ObsState state = connection.GetState();

        Assert.True(state.IsEnabled);
        Assert.False(state.IsConnected);
        Assert.False(state.IsStreaming);
    }

    [Fact]
    public async Task WhenDisconnectingAConnectionThatNeverConnected_ThenNoStateChangeIsRaised()
    {
        await using ObsWebSocketConnection connection = CreateConnection(new ObsOptions { Enabled = false });
        int stateChangedCount = 0;
        connection.StateChanged += (_, _) => stateChangedCount++;

        await connection.Disconnect(CancellationToken.None);

        Assert.Equal(0, stateChangedCount);
    }

    [Fact]
    public void WhenBuildingTheAuthenticationString_ThenTheObsWebSocketChallengeFormulaIsFollowed()
    {
        const string Password = "supersecretpassword";
        const string Salt = "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=";
        const string Challenge = "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY=";

        // The obs-websocket v5 spec, restated independently of the implementation:
        // base64(sha256(base64(sha256(password + salt)) + challenge)).
        string secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Password + Salt)));
        string expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + Challenge)));

        string authentication = ObsWebSocketConnection.BuildAuthenticationString(Password, Salt, Challenge);

        Assert.Equal(expected, authentication);
    }

    [Fact]
    public void WhenTheSaltAndChallengeAreSwapped_ThenADifferentAuthenticationStringIsProduced()
    {
        string correct = ObsWebSocketConnection.BuildAuthenticationString("password", "salt", "challenge");
        string swapped = ObsWebSocketConnection.BuildAuthenticationString("password", "challenge", "salt");

        Assert.NotEqual(correct, swapped);
    }

    private static ObsWebSocketConnection CreateConnection(ObsOptions options)
    {
        return new ObsWebSocketConnection(
            Options.Create(options),
            NullLogger<ObsWebSocketConnection>.Instance);
    }
}
