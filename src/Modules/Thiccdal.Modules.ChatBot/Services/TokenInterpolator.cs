using System.Globalization;
using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Replaces supported metadata tokens in chatbot response templates.
/// </summary>
public sealed class TokenInterpolator : ITokenInterpolator
{
    private readonly TimeProvider _timeProvider;

    public TokenInterpolator()
        : this(TimeProvider.System)
    {
    }

    public TokenInterpolator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Interpolate(string template, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        return template
            .Replace("{user}", context.UserDisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{platform}", context.Platform, StringComparison.OrdinalIgnoreCase)
            .Replace("{count}", context.UseCount.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{uptime}", GetUptimeTokenValue(context.StreamStartedAt), StringComparison.OrdinalIgnoreCase);
    }

    private string GetUptimeTokenValue(DateTimeOffset? streamStartedAt)
    {
        if (!streamStartedAt.HasValue)
        {
            return "offline";
        }

        TimeSpan uptime = _timeProvider.GetUtcNow() - streamStartedAt.Value;
        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        return FormatUptime(uptime);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        return uptime.TotalHours >= 1
            ? FormattableString.Invariant($"{(int)uptime.TotalHours}h {uptime.Minutes}m")
            : FormattableString.Invariant($"{uptime.Minutes}m {uptime.Seconds}s");
    }
}
