using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thiccdal.Teleprompter.Display;

public class DisplayConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ServerUrl { get; init; } = "https://localhost:5001";
    public string ViewPath { get; init; } = "/prompter";
    public int MonitorIndex { get; init; } = 1;
    public bool BlockMouse { get; init; } = true;
    public HotkeyConfig Hotkeys { get; init; } = new();
    public ObsConfig Obs { get; init; } = new();

    public DisplayConfig()
    {
    }

    public static string GetDefaultPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "displayconfig.json");
    }

    public static DisplayConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new DisplayConfig();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DisplayConfig>(json, SerializerOptions) ?? new DisplayConfig();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(path, json);
    }
}

public class HotkeyConfig
{
    public string ToggleDisplay { get; init; } = "Ctrl+Shift+T";

    public HotkeyConfig()
    {
    }
}

public class ObsConfig
{
    public bool Enabled { get; init; } = true;
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 4455;
    public string Password { get; init; } = "";
    public bool AutoStartOnStream { get; init; } = true;
    public bool AutoStopOnStreamEnd { get; init; } = true;

    public ObsConfig()
    {
    }
}
