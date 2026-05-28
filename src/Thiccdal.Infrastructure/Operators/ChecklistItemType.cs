namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Describes how a pre-live checklist item is satisfied.
/// </summary>
public enum ChecklistItemType
{
    Manual,
    Auto,
    AutoWithWarn,
    Action
}
