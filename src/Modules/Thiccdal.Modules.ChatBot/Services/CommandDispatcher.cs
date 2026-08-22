using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Dispatches normalized chat commands to registered handlers or static response templates.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private static readonly ConcurrentDictionary<string, Type?> _handlerTypeCache = new(StringComparer.Ordinal);
    private readonly ICommandRegistry _commandRegistry;
    private readonly ICommandResponseSink _commandResponseSink;
    private readonly ICommandUsageTracker _commandUsageTracker;
    private readonly ITokenInterpolator _tokenInterpolator;
    private readonly IOperatorStateService _operatorStateService;
    private readonly IChatBotAiResponder _chatBotAiResponder;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IPlatformConnection> _platformConnections;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        ICommandRegistry commandRegistry,
        ICommandResponseSink commandResponseSink,
        ICommandUsageTracker commandUsageTracker,
        ITokenInterpolator tokenInterpolator,
        IOperatorStateService operatorStateService,
        IChatBotAiResponder chatBotAiResponder,
        IServiceProvider serviceProvider,
        IEnumerable<IPlatformConnection> platformConnections,
        ILogger<CommandDispatcher> logger)
    {
        _commandRegistry = commandRegistry;
        _commandResponseSink = commandResponseSink;
        _commandUsageTracker = commandUsageTracker;
        _tokenInterpolator = tokenInterpolator;
        _operatorStateService = operatorStateService;
        _chatBotAiResponder = chatBotAiResponder;
        _serviceProvider = serviceProvider;
        _platformConnections = platformConnections;
        _logger = logger;
    }

    public async Task Dispatch(ChatEvent chatEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatEvent);
        cancellationToken.ThrowIfCancellationRequested();

        await _commandRegistry.Reload(cancellationToken);

        string content = chatEvent.Content.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        string[] messageParts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (messageParts.Length == 0)
        {
            return;
        }

        if (!content.StartsWith('!'))
        {
            await SendAiFallback(chatEvent, cancellationToken);
            return;
        }

        string trigger = NormalizeTrigger(messageParts[0]);
        if (string.Equals(trigger, "!commands", StringComparison.OrdinalIgnoreCase))
        {
            await SendBuiltInCommandsResponse(chatEvent, cancellationToken);
            return;
        }

        BotCommandDefinition? command = FindEnabledCommand(trigger);
        if (command is null)
        {
            await SendAiFallback(chatEvent, cancellationToken);
            return;
        }

        int useCount = await _commandUsageTracker.RecordUse(trigger, cancellationToken);
        await IncrementPersistedUseCount(trigger, cancellationToken);
        CommandContext context = CreateContext(chatEvent, trigger, messageParts.Skip(1).ToArray(), useCount);
        string? response = await ResolveResponse(command, context, cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        await _commandResponseSink.SendResponse(context, response, cancellationToken);
    }

    public async Task DispatchFromOperator(string trigger, CancellationToken cancellationToken = default)
    {
        string normalizedTrigger = NormalizeTrigger(trigger);
        cancellationToken.ThrowIfCancellationRequested();

        await _commandRegistry.Reload(cancellationToken);

        BotCommandDefinition? command = FindEnabledCommand(normalizedTrigger);
        if (command is null)
        {
            throw new InvalidOperationException($"The {normalizedTrigger} command is not available to run live.");
        }

        int useCount = await _commandUsageTracker.RecordUse(normalizedTrigger, cancellationToken);
        await IncrementPersistedUseCount(normalizedTrigger, cancellationToken);
        CommandContext context = CreateOperatorContext(normalizedTrigger, useCount);
        string? response = await ResolveResponse(command, context, cancellationToken);

        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        await SendOperatorResponse(response, cancellationToken);
    }

    private async Task SendBuiltInCommandsResponse(ChatEvent chatEvent, CancellationToken cancellationToken)
    {
        string[] triggers = _commandRegistry
            .GetEnabledCommands()
            .Select(command => command.Trigger)
            .Append("!commands")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static trigger => trigger, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CommandContext context = CreateContext(chatEvent, "!commands", [], 0);

        await _commandResponseSink.SendResponse(
            context,
            $"Available commands: {string.Join(", ", triggers)}",
            cancellationToken);
    }

    private async Task SendAiFallback(ChatEvent chatEvent, CancellationToken cancellationToken)
    {
        string? response = await _chatBotAiResponder.TryRespond(chatEvent, cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        await _commandResponseSink.SendResponse(
            CreateContext(chatEvent, "ai-mention", [], 0),
            response,
            cancellationToken);
    }

    private async Task<string?> ResolveResponse(
        BotCommandDefinition command,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        HandlerExecutionResult handlerExecutionResult = await ExecuteHandler(command, context, cancellationToken);
        if (handlerExecutionResult.SuppressStaticTemplate)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(handlerExecutionResult.Response))
        {
            return handlerExecutionResult.Response;
        }

        return string.IsNullOrWhiteSpace(command.ResponseTemplate)
            ? null
            : _tokenInterpolator.Interpolate(command.ResponseTemplate, context);
    }

    private async Task<HandlerExecutionResult> ExecuteHandler(
        BotCommandDefinition command,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.HandlerType))
        {
            return HandlerExecutionResult.UseStaticTemplate();
        }

        ICommandHandler? commandHandler = ResolveHandler(command.HandlerType, context.Trigger);
        if (commandHandler is null)
        {
            return HandlerExecutionResult.UseStaticTemplate();
        }

        try
        {
            string? response = await commandHandler.Handle(context, cancellationToken);
            return response is null
                ? HandlerExecutionResult.SuppressStaticResponse()
                : HandlerExecutionResult.UseHandlerResponse(response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Command handler {HandlerType} threw while dispatching {Trigger}.",
                command.HandlerType,
                context.Trigger);
            return HandlerExecutionResult.UseStaticTemplate();
        }
    }

    private ICommandHandler? ResolveHandler(string handlerTypeName, string trigger)
    {
        Type? handlerType = FindHandlerType(handlerTypeName);
        if (handlerType is null || !typeof(ICommandHandler).IsAssignableFrom(handlerType))
        {
            _logger.LogWarning(
                "Could not resolve command handler type {HandlerType} for {Trigger}.",
                handlerTypeName,
                trigger);
            return null;
        }

        object? service = _serviceProvider.GetService(handlerType);
        if (service is ICommandHandler commandHandler)
        {
            return commandHandler;
        }

        _logger.LogWarning(
            "Command handler {HandlerType} for {Trigger} is not registered in DI.",
            handlerTypeName,
            trigger);
        return null;
    }

    private BotCommandDefinition? FindEnabledCommand(string trigger)
    {
        return _commandRegistry
            .GetEnabledCommands()
            .FirstOrDefault(candidate => string.Equals(candidate.Trigger, trigger, StringComparison.OrdinalIgnoreCase));
    }

    private async Task IncrementPersistedUseCount(string trigger, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IBotCommandManagementService managementService = scope.ServiceProvider.GetRequiredService<IBotCommandManagementService>();
        await managementService.IncrementUseCount(trigger, cancellationToken);
    }

    private async Task SendOperatorResponse(string response, CancellationToken cancellationToken)
    {
        IPlatformConnection[] connectedPlatforms = _platformConnections
            .Where(platformConnection => platformConnection.Connected)
            .ToArray();

        if (connectedPlatforms.Length == 0)
        {
            throw new InvalidOperationException("No connected platforms are available to receive operator-triggered bot commands.");
        }

        foreach (IPlatformConnection platformConnection in connectedPlatforms)
        {
            await platformConnection.SendMessage(response, cancellationToken);
        }
    }

    private CommandContext CreateContext(ChatEvent chatEvent, string trigger, string[] args, int useCount)
    {
        return new CommandContext
        {
            Trigger = trigger,
            Args = args,
            UserDisplayName = chatEvent.Author,
            Platform = chatEvent.Source.ToString(),
            SourcePlatform = chatEvent.Source,
            ChannelId = chatEvent.Channel,
            UseCount = useCount,
            StreamStartedAt = _operatorStateService.GetActiveStreamState()?.StartedAt
        };
    }

    private CommandContext CreateOperatorContext(string trigger, int useCount)
    {
        return new CommandContext
        {
            Trigger = trigger,
            Args = [],
            UserDisplayName = "Operator",
            Platform = "All Platforms",
            SourcePlatform = PlatformEventSource.Null,
            ChannelId = null,
            UseCount = useCount,
            StreamStartedAt = _operatorStateService.GetActiveStreamState()?.StartedAt
        };
    }

    private static string NormalizeTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new InvalidOperationException("Command trigger is required.");
        }

        string normalizedTrigger = trigger.Trim();
        normalizedTrigger = normalizedTrigger.TrimStart('!');
        normalizedTrigger = normalizedTrigger.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalizedTrigger.Length == 0)
        {
            throw new InvalidOperationException("Command trigger is required.");
        }

        return $"!{normalizedTrigger.ToLowerInvariant()}";
    }

    private static Type? FindHandlerType(string handlerTypeName)
    {
        return _handlerTypeCache.GetOrAdd(handlerTypeName, static name =>
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? handlerType = assembly.GetType(name, throwOnError: false, ignoreCase: false);
                if (handlerType is not null)
                {
                    return handlerType;
                }
            }

            return null;
        });
    }

    private sealed record HandlerExecutionResult(string? Response, bool SuppressStaticTemplate)
    {
        public static HandlerExecutionResult UseHandlerResponse(string response)
        {
            return new HandlerExecutionResult(response, false);
        }

        public static HandlerExecutionResult UseStaticTemplate()
        {
            return new HandlerExecutionResult(null, false);
        }

        public static HandlerExecutionResult SuppressStaticResponse()
        {
            return new HandlerExecutionResult(null, true);
        }
    }
}
