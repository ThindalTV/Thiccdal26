namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Provides the catalog of installation wizard steps.
/// </summary>
public static class WizardStepCatalog
{
    /// <summary>
    /// Gets the ordered list of wizard steps.
    /// </summary>
    /// <returns>A read-only list of all wizard steps in order.</returns>
    public static IReadOnlyList<WizardStepModel> GetSteps() => new[]
    {
        new WizardStepModel(WizardStep.Welcome, "Welcome", "Introduction to Thiccdal setup", IsOptional: false),
        new WizardStepModel(WizardStep.Database, "Database", "Configure database connection", IsOptional: false),
        new WizardStepModel(WizardStep.Streaming, "Streaming", "Configure RTMP ingest and recording", IsOptional: false),
        new WizardStepModel(WizardStep.Platforms, "Platforms", "Connect streaming platforms", IsOptional: true),
        new WizardStepModel(WizardStep.AiSetup, "AI Setup", "Configure AI integration", IsOptional: true),
        new WizardStepModel(WizardStep.BotConfig, "Bot Config", "Set up chatbot settings", IsOptional: false),
        new WizardStepModel(WizardStep.Summary, "Summary", "Review and complete setup", IsOptional: false)
    };
}
