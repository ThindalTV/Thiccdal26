namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Provides synchronous access to the current operator-managed restream configuration snapshot.
/// </summary>
public interface IRestreamSettingsAccessor
{
    /// <summary>
    /// Returns the current restream configuration snapshot.
    /// </summary>
    RestreamConfigurationSnapshot GetCurrent();
}
