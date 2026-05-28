namespace Thiccdal.Data.Models;

public sealed class BotCommand
{
    public long Id { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public string ResponseTemplate { get; set; } = string.Empty;

    public string? HandlerType { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int UseCount { get; set; }
}
