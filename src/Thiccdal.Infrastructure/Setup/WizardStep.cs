namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Identifies each step in the installation wizard.
/// </summary>
public enum WizardStep
{
    Welcome = 0,
    Database = 1,
    Platforms = 2,
    AiSetup = 3,
    BotConfig = 4,
    Summary = 5
}
