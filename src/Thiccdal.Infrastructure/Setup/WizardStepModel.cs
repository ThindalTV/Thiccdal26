namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Represents a step in the installation wizard.
/// </summary>
/// <param name="Step">The wizard step identifier.</param>
/// <param name="Name">Display name for the step.</param>
/// <param name="Description">Brief description of what this step configures.</param>
/// <param name="IsOptional">Whether this step can be skipped.</param>
/// <param name="IsComplete">Whether this step has been completed.</param>
public sealed record WizardStepModel(
    WizardStep Step,
    string Name,
    string Description,
    bool IsOptional,
    bool IsComplete = false);
