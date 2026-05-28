using Thiccdal.Infrastructure.Operators;
using Thiccdal.Modules.Control.Components.Dialogs;

namespace Thiccdal.Tests;

public sealed class GoLiveChecklistSummaryFactoryTests
{
    [Fact]
    public void WhenBuildingSummary_ThenCheckedRequiredItemsStayInChecklistOrder()
    {
        PreLiveChecklistState checklistState = new()
        {
            Items =
            [
                CreateItem("platform-connection.twitch", "Platform Connections", "Twitch connected", true, true),
                CreateItem("stream-info.title", "Stream Info", "Stream title set", true, true),
                CreateItem("overlay.chat-feed", "Overlay Verification", "Chat Feed visible", false, true),
                CreateItem("personal.water-ready", "Personal Prep", "Water/drinks ready", false, false)
            ]
        };

        GoLiveChecklistSummary summary = GoLiveChecklistSummaryFactory.Create(checklistState);

        Assert.Equal(
            ["Twitch connected", "Stream title set"],
            summary.ConfirmedItems);
    }

    [Fact]
    public void WhenOptionalItemsRemainUnchecked_ThenWarningsIncludeCategoryAndRuntimeDetail()
    {
        PreLiveChecklistState checklistState = new()
        {
            Items =
            [
                CreateItem("platform-connection.twitch", "Platform Connections", "Twitch connected", true, true),
                CreateItem("personal.water-ready", "Personal Prep", "Water/drinks ready", false, false),
                CreateItem(
                    "recording-disk-space",
                    "Recording",
                    "Sufficient disk space (≥ 10 GB free)",
                    false,
                    false,
                    warningMessage: "Only 4.5 GB free on recording drive")
            ]
        };

        GoLiveChecklistSummary summary = GoLiveChecklistSummaryFactory.Create(checklistState);

        Assert.Equal(
            [
                "Personal Prep: \"Water/drinks ready\" not confirmed",
                "Recording: \"Sufficient disk space (≥ 10 GB free)\" not confirmed — Only 4.5 GB free on recording drive"
            ],
            summary.Warnings);
    }

    [Fact]
    public void WhenOptionalItemsAreAlreadyChecked_ThenWarningsAreHidden()
    {
        PreLiveChecklistState checklistState = new()
        {
            Items =
            [
                CreateItem("platform-connection.twitch", "Platform Connections", "Twitch connected", true, true),
                CreateItem("recording-path-configured", "Recording", "Recording output path configured", false, true)
            ]
        };

        GoLiveChecklistSummary summary = GoLiveChecklistSummaryFactory.Create(checklistState);

        Assert.Empty(summary.Warnings);
    }

    private static ChecklistItemState CreateItem(
        string id,
        string category,
        string label,
        bool isRequired,
        bool isChecked,
        string? warningMessage = null)
    {
        return new ChecklistItemState
        {
            Definition = new ChecklistItemDefinition
            {
                Id = id,
                Category = category,
                Label = label,
                IsRequired = isRequired
            },
            IsChecked = isChecked,
            WarningMessage = warningMessage
        };
    }
}
