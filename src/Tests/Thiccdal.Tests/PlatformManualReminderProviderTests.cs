using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.Overlay.Services;

namespace Thiccdal.Tests;

public sealed class PlatformManualReminderProviderTests
{
    [Fact]
    public void WhenGetReminders_ThenReturnsNonEmptyList()
    {
        // Arrange
        IPlatformManualReminderProvider provider = CreateProvider();

        // Act
        var reminders = provider.GetReminders();

        // Assert
        Assert.NotEmpty(reminders);
    }

    [Fact]
    public void WhenGetReminders_ThenReturnsTwitchReminders()
    {
        // Arrange
        IPlatformManualReminderProvider provider = CreateProvider();

        // Act
        var reminders = provider.GetReminders();
        var twitchReminders = reminders.Where(r => r.Platform == "Twitch").ToList();

        // Assert
        Assert.NotEmpty(twitchReminders);
        Assert.Contains(twitchReminders, r => r.Setting == "Stream encoding");
        Assert.Contains(twitchReminders, r => r.Setting == "Stream delay");
        Assert.Contains(twitchReminders, r => r.Setting == "Extensions");
        Assert.Contains(twitchReminders, r => r.Setting == "Ad schedule");
    }

    [Fact]
    public void WhenGetReminders_ThenOnlyTwitchIsListed()
    {
        // Arrange
        IPlatformManualReminderProvider provider = CreateProvider();

        // Act
        var reminders = provider.GetReminders();
        var platforms = reminders.Select(r => r.Platform).Distinct().ToList();

        // Assert
        Assert.Equal(["Twitch"], platforms);
    }

    [Fact]
    public void WhenGetReminders_ThenEachReminderHasRequiredFields()
    {
        // Arrange
        IPlatformManualReminderProvider provider = CreateProvider();

        // Act
        var reminders = provider.GetReminders();

        // Assert
        foreach (var reminder in reminders)
        {
            Assert.NotEmpty(reminder.Platform);
            Assert.NotEmpty(reminder.Setting);
            Assert.NotEmpty(reminder.ReminderText);
        }
    }

    [Fact]
    public void WhenGetRemindersCalled_ThenReturnsSameListEveryTime()
    {
        // Arrange
        IPlatformManualReminderProvider provider = CreateProvider();

        // Act
        var reminders1 = provider.GetReminders();
        var reminders2 = provider.GetReminders();

        // Assert
        Assert.Equal(reminders1.Count, reminders2.Count);
        for (int i = 0; i < reminders1.Count; i++)
        {
            Assert.Equal(reminders1[i].Platform, reminders2[i].Platform);
            Assert.Equal(reminders1[i].Setting, reminders2[i].Setting);
            Assert.Equal(reminders1[i].ReminderText, reminders2[i].ReminderText);
        }
    }

    private static IPlatformManualReminderProvider CreateProvider()
    {
        return new PlatformManualReminderProvider();
    }
}
