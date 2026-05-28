using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Routes chatbot responses back to the originating platform and channel.
/// </summary>
public sealed class ChatServiceCommandResponseSink : ICommandResponseSink
{
    private readonly IEnumerable<IPlatformConnection> _platformConnections;
    private readonly ILogger<ChatServiceCommandResponseSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatServiceCommandResponseSink"/> class.
    /// </summary>
    /// <param name="platformConnections">The currently registered platform connections.</param>
    /// <param name="logger">Writes reply-routing diagnostics.</param>
    public ChatServiceCommandResponseSink(
        IEnumerable<IPlatformConnection> platformConnections,
        ILogger<ChatServiceCommandResponseSink> logger)
    {
        ArgumentNullException.ThrowIfNull(platformConnections);
        ArgumentNullException.ThrowIfNull(logger);

        _platformConnections = platformConnections;
        _logger = logger;
    }

    public Task SendResponse(CommandContext context, string response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        IPlatformConnection? platformConnection = _platformConnections.FirstOrDefault(
            candidate => candidate.Connected &&
                string.Equals(candidate.PlatformName, context.SourcePlatform.ToString(), StringComparison.OrdinalIgnoreCase));

        if (platformConnection is null)
        {
            _logger.LogWarning(
                "Could not route chatbot response to {Platform}/{Channel}. No connected platform matched the originating source.",
                context.SourcePlatform,
                context.ChannelId);
            return Task.CompletedTask;
        }

        return platformConnection.SendMessage(response, context.ChannelId, cancellationToken);
    }
}
