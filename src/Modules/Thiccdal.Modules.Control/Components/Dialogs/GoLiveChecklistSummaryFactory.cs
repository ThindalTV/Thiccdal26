using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Modules.Control.Components.Dialogs;

internal sealed record GoLiveChecklistSummary
{
    public IReadOnlyList<string> ConfirmedItems { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

internal static class GoLiveChecklistSummaryFactory
{
    public static GoLiveChecklistSummary Create(PreLiveChecklistState checklistState)
    {
        ArgumentNullException.ThrowIfNull(checklistState);

        return new GoLiveChecklistSummary
        {
            ConfirmedItems =
            [
                .. checklistState.Items
                    .Where(static item => item.Definition.IsRequired && item.IsChecked)
                    .Select(static item => item.Definition.Label)
            ],
            Warnings =
            [
                .. checklistState.Items
                    .Where(static item => !item.Definition.IsRequired && !item.IsChecked)
                    .Select(BuildWarningText)
            ]
        };
    }

    private static string BuildWarningText(ChecklistItemState item)
    {
        string warning = $"{item.Definition.Category}: \"{item.Definition.Label}\" not confirmed";

        if (!string.IsNullOrWhiteSpace(item.WarningMessage))
        {
            warning = $"{warning} — {item.WarningMessage}";
        }

        return warning;
    }
}
