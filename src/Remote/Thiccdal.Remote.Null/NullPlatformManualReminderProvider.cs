using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Null;

internal sealed class NullPlatformManualReminderProvider : IPlatformManualReminderProvider
{
    public IReadOnlyList<PlatformManualReminder> GetReminders()
    {
        return Array.Empty<PlatformManualReminder>();
    }
}
