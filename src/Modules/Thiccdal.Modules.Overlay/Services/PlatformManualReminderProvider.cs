using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.Overlay.Services;

internal sealed class PlatformManualReminderProvider : IPlatformManualReminderProvider
{
    private static readonly IReadOnlyList<PlatformManualReminder> Reminders =
    [
        new() { Platform = "Twitch", Setting = "Stream encoding", ReminderText = "Set bitrate, resolution & keyframe interval in OBS" },
        new() { Platform = "Twitch", Setting = "Stream delay", ReminderText = "Enable/configure stream delay in Creator Dashboard if needed" },
        new() { Platform = "Twitch", Setting = "Extensions", ReminderText = "Activate/configure extensions in Creator Dashboard" },
        new() { Platform = "Twitch", Setting = "Ad schedule", ReminderText = "Configure ad schedule in Creator Dashboard" }
    ];

    public IReadOnlyList<PlatformManualReminder> GetReminders()
    {
        return Reminders;
    }
}
