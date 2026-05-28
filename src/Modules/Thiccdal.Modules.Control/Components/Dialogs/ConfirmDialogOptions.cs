namespace Thiccdal.Modules.Control.Components.Dialogs;

internal sealed record ConfirmDialogOptions
{
    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string ConfirmText { get; init; } = "Confirm";

    public ConfirmStyle ConfirmStyle { get; init; } = ConfirmStyle.Primary;

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
