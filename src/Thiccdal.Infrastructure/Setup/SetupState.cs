namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Represents the overall state of the application setup wizard.
/// </summary>
public enum SetupState
{
    /// <summary>Setup has not been started.</summary>
    NotStarted,

    /// <summary>Setup is currently in progress.</summary>
    InProgress,

    /// <summary>Setup has been completed.</summary>
    Complete
}
