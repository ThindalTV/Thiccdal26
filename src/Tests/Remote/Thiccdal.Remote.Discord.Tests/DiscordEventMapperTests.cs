using Xunit;

namespace Thiccdal.Remote.Discord.Tests;

/// <summary>
/// Tests for DiscordEventMapper.
/// Note: Due to Discord.Net's sealed types (SocketMessage, SocketUser, etc.),
/// comprehensive testing requires integration tests with a live bot connection.
/// These tests verify basic mapper structure only.
/// </summary>
public sealed class DiscordEventMapperTests
{
    [Fact]
    public void DiscordEventMapper_IsPublic()
    {
        // Verify the mapper is accessible from test assembly
        var mapperType = typeof(DiscordEventMapper);
        Assert.True(mapperType.IsPublic);
        Assert.True(mapperType.IsAbstract); // static class
        Assert.True(mapperType.IsSealed);
    }
}
