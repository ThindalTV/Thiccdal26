using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data;

/// <summary>
/// Builds bounded, filtered chatter memory from persisted public chat messages.
/// </summary>
public sealed class ChatterMemoryService : IChatterMemoryService
{
    private static readonly Regex UrlPattern = new(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TokenPattern = new(@"\b[A-Za-z0-9_\-]{24,}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PreferencePattern = new(
        @"\b(?:i\s+(?:really\s+)?(?:like|love|prefer|enjoy)|my\s+favorite(?:\s+\w+)?\s+is|i(?:'m| am)\s+into)\s+(?<topic>[a-z0-9][a-z0-9\s'\-]{1,40})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords =
    [
        "about", "after", "again", "been", "being", "because", "before", "chat", "could", "didn", "does", "doing",
        "from", "game", "have", "here", "just", "like", "love", "maybe", "more", "much", "next", "only", "really",
        "schedule", "should", "some", "stream", "that", "their", "there", "these", "they", "this", "today", "want",
        "what", "when", "where", "which", "while", "with", "would", "your", "youre"
    ];

    private static readonly string[] SensitiveMarkers =
    [
        "address", "anxiety", "bank", "bipolar", "campaign", "cancer", "card number", "candidate", "conservative",
        "credit card", "debit card", "depressed", "depression", "diagnosis", "discord.gg", "donate at", "election",
        "gmail.com", "hospital", "immigrant", "liberal", "location", "medicine", "medication", "muslim", "password",
        "paypal", "phone number", "politic", "pregnan", "president", "religion", "republican", "social security",
        "street", "token", "transgender", "wallet", "zipcode"
    ];

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IOptions<ChatBotOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChatterMemoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatterMemoryService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Creates database contexts for memory lookups.</param>
    /// <param name="options">Provides chatbot memory settings.</param>
    /// <param name="timeProvider">Supplies the current time for retention checks.</param>
    /// <param name="logger">Writes chatter-memory diagnostics.</param>
    public ChatterMemoryService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IOptions<ChatBotOptions> options,
        TimeProvider timeProvider,
        ILogger<ChatterMemoryService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatterMemoryContext?> GetMemoryContext(
        PlatformEventSource source,
        string channel,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime? resetCutoff = await GetResetCutoffUtc(dbContext, source, channel, platformUserId, cancellationToken);

        PlatformUser? platformUser = await dbContext.PlatformUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Source == source && user.PlatformUserId == platformUserId,
                cancellationToken);

        if (platformUser is null)
        {
            return null;
        }

        IQueryable<ChatMessage> scopedMessages = dbContext.ChatMessages
            .AsNoTracking()
            .Include(chatMessage => chatMessage.PlatformEvent)
            .Where(
                chatMessage => chatMessage.Source == source
                    && chatMessage.PlatformUserId == platformUser.Id
                    && chatMessage.PlatformEvent.Channel == channel);

        DateTime? retentionCutoff = GetRetentionCutoffUtc();
        if (retentionCutoff.HasValue)
        {
            scopedMessages = scopedMessages.Where(chatMessage => chatMessage.SentAt >= retentionCutoff.Value);
        }

        if (resetCutoff.HasValue)
        {
            scopedMessages = scopedMessages.Where(chatMessage => chatMessage.SentAt > resetCutoff.Value);
        }

        List<ChatMessage> messages = await scopedMessages
            .OrderByDescending(chatMessage => chatMessage.SentAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return null;
        }

        List<string> facts = BuildFacts(messages);
        if (facts.Count == 0)
        {
            return null;
        }

        DateTime lastInteractionAt = messages.Max(static chatMessage => chatMessage.SentAt);
        return new ChatterMemoryContext(platformUser.DisplayName, lastInteractionAt, facts);
    }

    /// <inheritdoc />
    public async Task Reset(
        PlatformEventSource source,
        string channel,
        string platformUserId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime resetAt = _timeProvider.GetUtcNow().UtcDateTime;
        dbContext.ChatterMemoryResets.Add(
            new ChatterMemoryReset
            {
                Source = source.ToString(),
                Channel = channel,
                PlatformUserId = platformUserId,
                RequestedBy = requestedBy,
                ResetAt = resetAt
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reset chatter memory for {Platform}/{Channel}/{PlatformUserId} at {ResetAt}. Requested by {RequestedBy}. Source chat history was preserved.",
            source,
            channel,
            platformUserId,
            resetAt,
            requestedBy);
    }

    /// <inheritdoc />
    public async Task ResetAll(string requestedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime resetAt = _timeProvider.GetUtcNow().UtcDateTime;
        dbContext.ChatterMemoryResets.Add(
            new ChatterMemoryReset
            {
                RequestedBy = requestedBy,
                ResetAt = resetAt
            });
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Reset all chatter memory at {ResetAt}. Requested by {RequestedBy}. Source chat history was preserved.",
            resetAt,
            requestedBy);
    }

    private DateTime? GetRetentionCutoffUtc()
    {
        int? retentionDays = _options.Value.AiResponder.ChatterMemoryRetentionDays;
        if (!retentionDays.HasValue)
        {
            return null;
        }

        return _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays.Value);
    }

    private static async Task<DateTime?> GetResetCutoffUtc(
        ApplicationDbContext dbContext,
        PlatformEventSource source,
        string channel,
        string platformUserId,
        CancellationToken cancellationToken)
    {
        string sourceKey = source.ToString();
        return await dbContext.ChatterMemoryResets
            .AsNoTracking()
            .Where(
                reset => (reset.Source == null && reset.Channel == null && reset.PlatformUserId == null)
                    || (reset.Source == sourceKey
                        && reset.Channel == channel
                        && reset.PlatformUserId == platformUserId))
            .MaxAsync(static reset => (DateTime?)reset.ResetAt, cancellationToken);
    }

    private static List<string> BuildFacts(List<ChatMessage> messages)
    {
        List<string> facts = [];
        HashSet<string> seenFacts = new(StringComparer.OrdinalIgnoreCase);
        List<string> sanitizedMessages = [];

        foreach (ChatMessage message in messages.OrderByDescending(static chatMessage => chatMessage.SentAt))
        {
            string sanitized = SanitizeContent(message.Content);
            if (string.IsNullOrWhiteSpace(sanitized) || ContainsSensitiveContent(sanitized))
            {
                continue;
            }

            sanitizedMessages.Add(sanitized);

            foreach (string preference in ExtractPreferences(sanitized))
            {
                if (seenFacts.Add(preference))
                {
                    facts.Add(preference);
                }

                if (facts.Count == 3)
                {
                    return facts;
                }
            }
        }

        string? topicsFact = BuildTopicsFact(sanitizedMessages);
        if (!string.IsNullOrWhiteSpace(topicsFact) && seenFacts.Add(topicsFact))
        {
            facts.Add(topicsFact);
        }

        return facts.Take(3).ToList();
    }

    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string normalized = content.Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Length > 160 ? normalized[..160] : normalized;
    }

    private static bool ContainsSensitiveContent(string content)
    {
        if (UrlPattern.IsMatch(content) || TokenPattern.IsMatch(content))
        {
            return true;
        }

        string lowered = content.ToLowerInvariant();
        return SensitiveMarkers.Any(lowered.Contains);
    }

    private static IEnumerable<string> ExtractPreferences(string content)
    {
        foreach (Match match in PreferencePattern.Matches(content))
        {
            string topic = NormalizeTopic(match.Groups["topic"].Value);
            if (string.IsNullOrWhiteSpace(topic) || ContainsSensitiveContent(topic))
            {
                continue;
            }

            yield return $"likes {topic}";
        }
    }

    private static string? BuildTopicsFact(List<string> sanitizedMessages)
    {
        Dictionary<string, int> topicCounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (string message in sanitizedMessages)
        {
            foreach (string token in Tokenize(message))
            {
                topicCounts[token] = topicCounts.GetValueOrDefault(token, 0) + 1;
            }
        }

        string[] topics = topicCounts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(static pair => pair.Key)
            .ToArray();

        return topics.Length == 0
            ? null
            : $"recent topics: {string.Join(", ", topics)}";
    }

    private static IEnumerable<string> Tokenize(string content)
    {
        foreach (string token in Regex.Split(content.ToLowerInvariant(), @"[^a-z0-9]+"))
        {
            if (token.Length < 4 || StopWords.Contains(token))
            {
                continue;
            }

            yield return token;
        }
    }

    private static string NormalizeTopic(string topic)
    {
        string normalized = Regex.Replace(topic.Trim().ToLowerInvariant(), @"\s+", " ");
        normalized = normalized.Trim(' ', '.', ',', '!', '?', ';', ':', '"', '\'');

        if (normalized.StartsWith("that ", StringComparison.Ordinal) ||
            normalized.StartsWith("the ", StringComparison.Ordinal) ||
            normalized.StartsWith("this ", StringComparison.Ordinal))
        {
            normalized = normalized[(normalized.IndexOf(' ') + 1)..];
        }

        return normalized;
    }
}
