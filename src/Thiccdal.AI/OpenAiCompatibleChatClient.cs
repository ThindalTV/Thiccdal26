using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Thiccdal.Infrastructure.AI;

namespace Thiccdal.AI;

/// <summary>
/// Sends chat-completion requests to an OpenAI-compatible endpoint.
/// </summary>
public sealed class OpenAiCompatibleChatClient : IChatCompletionClient
{
    private const string DefaultLocalApiKey = "lm-studio";

    private readonly OpenAIClient _openAiClient;
    private readonly TimeSpan _requestTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatibleChatClient"/> class.
    /// </summary>
    /// <param name="options">Provides the configured OpenAI-compatible endpoint settings.</param>
    public OpenAiCompatibleChatClient(IOptions<OpenAiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        OpenAiOptions configuredOptions = options.Value;
        string apiKey = string.IsNullOrWhiteSpace(configuredOptions.ApiKey)
            ? DefaultLocalApiKey
            : configuredOptions.ApiKey.Trim();

        _openAiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = CreateEndpoint(configuredOptions.Endpoint)
            });

        _requestTimeout = TimeSpan.FromSeconds(configuredOptions.RequestTimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<AiChatCompletionResult> CompleteChat(
        AiChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new ArgumentException("An AI model is required.", nameof(request));
        }

        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one AI chat message is required.", nameof(request));
        }

        using CancellationTokenSource linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellationTokenSource.CancelAfter(_requestTimeout);

        ChatClient chatClient = _openAiClient.GetChatClient(request.Model);
        List<ChatMessage> messages = new List<ChatMessage>(request.Messages.Count);
        foreach (AiChatMessage message in request.Messages)
        {
            messages.Add(MapMessage(message));
        }

        ChatCompletionOptions options = new ChatCompletionOptions();
        if (request.Temperature.HasValue)
        {
            options.Temperature = (float)request.Temperature.Value;
        }

        if (request.MaxOutputTokenCount.HasValue)
        {
            options.MaxOutputTokenCount = request.MaxOutputTokenCount.Value;
        }

        ChatCompletion completion = await chatClient.CompleteChatAsync(
            messages,
            options,
            linkedCancellationTokenSource.Token);

        string content = string.Concat(completion.Content.Select(static part => part.Text ?? string.Empty));
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The OpenAI-compatible endpoint did not return assistant content.");
        }

        return new AiChatCompletionResult(
            content,
            request.Model,
            completion.FinishReason.ToString());
    }

    private static Uri CreateEndpoint(string endpoint)
    {
        string normalizedEndpoint = endpoint.TrimEnd('/');
        return new Uri(normalizedEndpoint, UriKind.Absolute);
    }

    private static ChatMessage MapMessage(AiChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Role switch
        {
            AiChatMessageRole.System => new SystemChatMessage(message.Content),
            AiChatMessageRole.User => new UserChatMessage(message.Content),
            AiChatMessageRole.Assistant => new AssistantChatMessage(message.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Role, "Unsupported AI chat role.")
        };
    }
}
