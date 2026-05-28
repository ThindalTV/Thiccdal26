namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Provides the hardcoded list of manual platform settings reminders.
/// </summary>
public interface IPlatformManualReminderProvider
{
    /// <summary>
    /// Gets all manual platform reminders.
    /// </summary>
    /// <returns>A read-only list of platform manual reminders.</returns>
    IReadOnlyList<PlatformManualReminder> GetReminders();
}
