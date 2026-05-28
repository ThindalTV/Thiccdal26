using System.Collections.Concurrent;
using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Tracks in-session command usage counts until a persistent backing implementation is provided.
/// </summary>
public sealed class InMemoryCommandUsageTracker : ICommandUsageTracker
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public Task<int> RecordUse(string trigger, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        int useCount = _counts.AddOrUpdate(
            trigger.Trim(),
            1,
            static (_, currentCount) => checked(currentCount + 1));

        return Task.FromResult(useCount);
    }
}
