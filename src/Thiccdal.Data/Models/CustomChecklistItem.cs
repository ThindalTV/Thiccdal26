namespace Thiccdal.Data.Models;

public sealed class CustomChecklistItem
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsEnabled { get; set; } = true;
}
