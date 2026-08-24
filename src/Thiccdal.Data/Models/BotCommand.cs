namespace Thiccdal.Data.Models;

public sealed class BotCommand
{
    public long Id { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public string ResponseTemplate { get; set; } = string.Empty;

    public string? HandlerType { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int UseCount { get; set; }

    public bool SendInChat { get; set; } = true;

    public bool ShowOnLowerThird { get; set; }

    public string? LowerThirdTitle { get; set; }

    public string? LowerThirdText { get; set; }
}
