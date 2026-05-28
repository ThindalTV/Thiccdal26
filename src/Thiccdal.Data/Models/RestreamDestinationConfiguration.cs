namespace Thiccdal.Data.Models;

public sealed class RestreamDestinationConfiguration
{
    public int Id { get; set; }

    public string PlatformName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }
}
