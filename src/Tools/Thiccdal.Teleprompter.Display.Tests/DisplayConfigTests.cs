using System.IO;
using Xunit;

namespace Thiccdal.Teleprompter.Display.Tests;

public class DisplayConfigTests
{
    [Fact]
    public void WhenCreatingDefaultConfig_ThenHasExpectedDefaults()
    {
        var config = new DisplayConfig();

        Assert.Equal("https://localhost:5001", config.ServerUrl);
        Assert.Equal("/prompter", config.ViewPath);
        Assert.Equal(1, config.MonitorIndex);
        Assert.True(config.BlockMouse);
        Assert.NotNull(config.Hotkeys);
        Assert.NotNull(config.Obs);
    }

    [Fact]
    public void WhenCreatingDefaultHotkeyConfig_ThenHasExpectedDefaults()
    {
        var config = new HotkeyConfig();

        Assert.Equal("Ctrl+Shift+T", config.ToggleDisplay);
    }

    [Fact]
    public void WhenCreatingDefaultObsConfig_ThenHasExpectedDefaults()
    {
        var config = new ObsConfig();

        Assert.True(config.Enabled);
        Assert.Equal("localhost", config.Host);
        Assert.Equal(4455, config.Port);
        Assert.Equal("", config.Password);
        Assert.True(config.AutoStartOnStream);
        Assert.True(config.AutoStopOnStreamEnd);
    }

    [Fact]
    public void WhenSavingAndLoadingConfig_ThenRoundTripsCorrectly()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"displayconfig-{Guid.NewGuid()}.json");

        try
        {
            var original = new DisplayConfig
            {
                ServerUrl = "https://test.example.com",
                ViewPath = "/custom-path",
                MonitorIndex = 2,
                BlockMouse = false,
                Hotkeys = new HotkeyConfig { ToggleDisplay = "Ctrl+Alt+P" },
                Obs = new ObsConfig
                {
                    Enabled = false,
                    Host = "192.168.1.100",
                    Port = 4444,
                    Password = "secret",
                    AutoStartOnStream = false,
                    AutoStopOnStreamEnd = false
                }
            };

            original.Save(tempPath);
            var loaded = DisplayConfig.Load(tempPath);

            Assert.Equal(original.ServerUrl, loaded.ServerUrl);
            Assert.Equal(original.ViewPath, loaded.ViewPath);
            Assert.Equal(original.MonitorIndex, loaded.MonitorIndex);
            Assert.Equal(original.BlockMouse, loaded.BlockMouse);
            Assert.Equal(original.Hotkeys.ToggleDisplay, loaded.Hotkeys.ToggleDisplay);
            Assert.Equal(original.Obs.Enabled, loaded.Obs.Enabled);
            Assert.Equal(original.Obs.Host, loaded.Obs.Host);
            Assert.Equal(original.Obs.Port, loaded.Obs.Port);
            Assert.Equal(original.Obs.Password, loaded.Obs.Password);
            Assert.Equal(original.Obs.AutoStartOnStream, loaded.Obs.AutoStartOnStream);
            Assert.Equal(original.Obs.AutoStopOnStreamEnd, loaded.Obs.AutoStopOnStreamEnd);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void WhenLoadingNonExistentFile_ThenReturnsDefaultConfig()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.json");

        var config = DisplayConfig.Load(nonExistentPath);

        Assert.Equal("https://localhost:5001", config.ServerUrl);
        Assert.Equal("/prompter", config.ViewPath);
    }

    [Fact]
    public void WhenSerializingConfig_ThenUsesCamelCase()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"displayconfig-{Guid.NewGuid()}.json");

        try
        {
            var config = new DisplayConfig();
            config.Save(tempPath);

            var json = File.ReadAllText(tempPath);

            Assert.Contains("serverUrl", json);
            Assert.Contains("viewPath", json);
            Assert.Contains("monitorIndex", json);
            Assert.Contains("blockMouse", json);
            Assert.DoesNotContain("ServerUrl", json);
            Assert.DoesNotContain("ViewPath", json);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void WhenLoadingPartialConfig_ThenMergesWithDefaults()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"displayconfig-{Guid.NewGuid()}.json");

        try
        {
            // Write a partial config with only some fields
            var partialJson = """
                {
                    "serverUrl": "https://custom.server.com",
                    "monitorIndex": 3
                }
                """;
            File.WriteAllText(tempPath, partialJson);

            var config = DisplayConfig.Load(tempPath);

            Assert.Equal("https://custom.server.com", config.ServerUrl);
            Assert.Equal(3, config.MonitorIndex);
            // Defaults should be applied for missing fields
            Assert.Equal("/prompter", config.ViewPath);
            Assert.True(config.BlockMouse);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void WhenGetDefaultPath_ThenReturnsPathInAppDirectory()
    {
        var defaultPath = DisplayConfig.GetDefaultPath();

        Assert.EndsWith("displayconfig.json", defaultPath);
        Assert.Contains(AppContext.BaseDirectory, defaultPath);
    }
}
