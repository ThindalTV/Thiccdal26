using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public sealed class TwitchTargetChannelService : ITwitchTargetChannelService
{
    private const int ConfigurationId = 1;

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<TwitchTargetChannelService> _logger;

    public event EventHandler<TwitchChatConnectionProfile>? ConnectionProfileChanged;

    public TwitchTargetChannelService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<TwitchTargetChannelService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<TwitchChatConnectionProfile> GetConnectionProfile(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        TwitchTargetChannelConfiguration? configuration = await context.TwitchTargetChannels
            .AsNoTracking()
            .SingleOrDefaultAsync(target => target.Id == ConfigurationId, cancellationToken);

        return await BuildProfile(configuration, cancellationToken);
    }

    public async Task<TwitchChatConnectionProfile> UpdateTargetChannel(
        TwitchTargetChannelSettings targetChannel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetChannel);

        TwitchChatConnectionProfile previousProfile = await GetConnectionProfile(cancellationToken);
        string normalizedChannel = NormalizeRequiredChannel(targetChannel.TargetChannel);
        string normalizedBroadcasterId = NormalizeBroadcasterId(targetChannel.BroadcasterId);

        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        TwitchTargetChannelConfiguration? configuration = await context.TwitchTargetChannels
            .SingleOrDefaultAsync(current => current.Id == ConfigurationId, cancellationToken);

        if (configuration == null)
        {
            configuration = new TwitchTargetChannelConfiguration
            {
                Id = ConfigurationId
            };

            context.TwitchTargetChannels.Add(configuration);
        }

        configuration.TargetChannel = normalizedChannel;
        configuration.BroadcasterId = normalizedBroadcasterId;
        configuration.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        TwitchChatConnectionProfile updatedProfile = await BuildProfile(configuration, cancellationToken);
        if (updatedProfile == previousProfile)
        {
            return updatedProfile;
        }

        _logger.LogInformation(
            "Updated Twitch target channel to {TargetChannel} for bot account {BotUsername}",
            updatedProfile.TargetChannel,
            updatedProfile.BotUsername);

        ConnectionProfileChanged?.Invoke(this, updatedProfile);
        return updatedProfile;
    }

    private async Task<TwitchChatConnectionProfile> BuildProfile(TwitchTargetChannelConfiguration? configuration, CancellationToken cancellationToken)
    {
        await using ApplicationDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        TwitchToken? token = await context.TwitchTokens
            .AsNoTracking()
            .OrderByDescending(static t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string botUsername = NormalizeOptionalValue(token?.Username);
        string botUserId = NormalizeOptionalValue(token?.UserId);
        string targetChannel = NormalizeOptionalChannel(configuration?.TargetChannel);
        string broadcasterId = NormalizeBroadcasterId(configuration?.BroadcasterId);

        return new TwitchChatConnectionProfile
        {
            BotUsername = botUsername,
            BotUserId = botUserId,
            TargetChannel = targetChannel,
            BroadcasterId = broadcasterId
        };
    }

    private static string NormalizeRequiredChannel(string channel)
    {
        string normalized = NormalizeOptionalChannel(channel);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A Twitch target channel is required.", nameof(channel));
        }

        if (normalized.Any(static character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException(
                "Twitch channel names can only include letters, numbers, and underscores.",
                nameof(channel));
        }

        return normalized;
    }

    private static string NormalizeOptionalChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return string.Empty;
        }

        return channel.Trim().TrimStart('#', '@').ToLowerInvariant();
    }

    private static string NormalizeBroadcasterId(string? broadcasterId) => NormalizeOptionalValue(broadcasterId);

    private static string NormalizeOptionalValue(string? value) => value?.Trim() ?? string.Empty;
}
