using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data;

/// <summary>
/// Persists operator checklist snapshots for later per-session auditing.
/// </summary>
public sealed class ChecklistSessionService : IChecklistSessionService
{
    private const string BlockedStatus = "Blocked";
    private const string CheckedStatus = "Checked";
    private const string UncheckedStatus = "Unchecked";
    private const string WarnedStatus = "Warned";

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChecklistSessionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecklistSessionService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Creates database contexts for checklist snapshot persistence.</param>
    /// <param name="timeProvider">Supplies the current time for snapshot timestamps.</param>
    /// <param name="logger">Writes checklist snapshot diagnostics.</param>
    public ChecklistSessionService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        TimeProvider timeProvider,
        ILogger<ChecklistSessionService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Save(Guid sessionId, IPreLiveChecklistService checklist, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(checklist);

        PreLiveChecklistState snapshot = checklist.GetState();
        ChecklistItemState[] items = [.. snapshot.Items];
        DateTimeOffset recordedAt = _timeProvider.GetUtcNow();

        ChecklistSession checklistSession = new()
        {
            SessionId = sessionId,
            RecordedAt = recordedAt,
            Items = items
                .Select(
                    item => new ChecklistSessionItem
                    {
                        ItemId = item.Definition.Id,
                        Category = item.Definition.Category,
                        Label = item.Definition.Label,
                        Status = GetStatus(item),
                        IsRequired = item.Definition.IsRequired,
                        WarningMessage = GetWarningMessage(item)
                    })
                .ToArray()
        };

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ChecklistSessions.Add(checklistSession);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Saved checklist audit snapshot for session {SessionId} with {ItemCount} items at {RecordedAt}",
            sessionId,
            items.Length,
            recordedAt);
    }

    private static string GetStatus(ChecklistItemState item)
    {
        if (item.IsChecked)
        {
            return CheckedStatus;
        }

        if (item.IsBlocked)
        {
            return BlockedStatus;
        }

        if (item.IsWarning || !item.Definition.IsRequired || item.Definition.Type == ChecklistItemType.AutoWithWarn)
        {
            return WarnedStatus;
        }

        return UncheckedStatus;
    }

    private static string? GetWarningMessage(ChecklistItemState item)
    {
        if (!string.IsNullOrWhiteSpace(item.WarningMessage))
        {
            return item.WarningMessage;
        }

        if (item.IsBlocked)
        {
            return "Checklist item was blocked when the operator confirmed go live.";
        }

        if (!item.Definition.IsRequired && !item.IsChecked)
        {
            return "Optional checklist item remained unchecked when the operator confirmed go live.";
        }

        if (item.Definition.Type == ChecklistItemType.AutoWithWarn && !item.IsChecked)
        {
            return "Checklist item reported a warning when the operator confirmed go live.";
        }

        return null;
    }
}
