using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.Overlay.Services;

internal sealed class PlatformManualReminderProvider : IPlatformManualReminderProvider
{
    private static readonly IReadOnlyList<PlatformManualReminder> Reminders =
    [
        new() { Platform = "Twitch", Setting = "Stream encoding", ReminderText = "Set bitrate, resolution & keyframe interval in OBS" },
        new() { Platform = "Twitch", Setting = "Stream delay", ReminderText = "Enable/configure stream delay in Creator Dashboard if needed" },
        new() { Platform = "Twitch", Setting = "Extensions", ReminderText = "Activate/configure extensions in Creator Dashboard" },
        new() { Platform = "Twitch", Setting = "Ad schedule", ReminderText = "Configure ad schedule in Creator Dashboard" },
        new() { Platform = "YouTube", Setting = "Made for Kids", ReminderText = "Confirm 'Made for Kids' setting in YouTube Studio" },
        new() { Platform = "YouTube", Setting = "Super Chat", ReminderText = "Verify Super Chat & Super Thanks are enabled in YouTube Studio" },
        new() { Platform = "YouTube", Setting = "Visibility", ReminderText = "Set visibility to Public in YouTube Studio when ready" },
        new() { Platform = "YouTube", Setting = "Age restriction", ReminderText = "Review age restriction setting in YouTube Studio" },
        new() { Platform = "Discord", Setting = "Stream permissions", ReminderText = "Configure who can view the stream in server/channel settings" },
        new() { Platform = "Discord", Setting = "NSFW", ReminderText = "Review NSFW channel flag in server settings" },
        new() { Platform = "LinkedIn", Setting = "All settings", ReminderText = "LinkedIn Live settings must be configured in LinkedIn Studio" },
        new() { Platform = "Facebook", Setting = "Privacy", ReminderText = "Set broadcast privacy (Timeline / Page / Group) before going live — cannot change mid-stream" },
        new() { Platform = "Facebook", Setting = "App Review", ReminderText = "Confirm Live Video permissions have passed App Review" },
        new() { Platform = "X", Setting = "Broadcast Tweet", ReminderText = "Compose the broadcast Tweet text before starting — cannot edit after stream begins" },
        new() { Platform = "X", Setting = "API tier", ReminderText = "Verify X API write access tier is active (Basic or higher required)" },
        new() { Platform = "TikTok", Setting = "All settings", ReminderText = "TikTok Live settings must be configured in TikTok Studio — API access pending approval" }
    ];

    public IReadOnlyList<PlatformManualReminder> GetReminders()
    {
        return Reminders;
    }
}
