namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Identifies each step in the installation wizard.
/// </summary>
public enum WizardStep
{
    Welcome = 0,
    Database = 1,
    Streaming = 2,
    Platforms = 3,
    AiSetup = 4,
    BotConfig = 5,
    Summary = 6
}
