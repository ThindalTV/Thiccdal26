using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;

namespace Thiccdal.Remote.LMStudio;

/// <summary>
/// Calls LM Studio's OpenAI-compatible chat completions endpoint.
/// </summary>
public sealed class LmStudioClient : ILmStudioClient
{
    private readonly ILogger<LmStudioClient> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="LmStudioClient"/> class.
    /// </summary>
    /// <param name="options">The configured LM Studio options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClientFactory">Creates named HTTP clients.</param>
    public LmStudioClient(
        IOptions<LmStudioOptions> options,
        ILogger<LmStudioClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(LmStudioClientNames.Default);

        string apiKey = options.Value.ApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    /// <inheritdoc />
    public async Task<LmStudioChatCompletionResult> CompleteChat(
        LmStudioChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new ArgumentException("An LM Studio model is required.", nameof(request));
        }

        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one LM Studio chat message is required.", nameof(request));
        }

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "v1/chat/completions",
            new ChatCompletionHttpRequest(
                request.Model,
                request.Messages.Select(static message => new ChatCompletionHttpMessage(message.Role, message.Content)).ToArray(),
                request.Temperature,
                request.MaxTokens),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string failureBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "LM Studio chat completion returned HTTP {StatusCode} for model {Model}. Body: {Body}",
                (int)response.StatusCode,
                request.Model,
                failureBody);

            response.EnsureSuccessStatusCode();
        }

        ChatCompletionHttpResponse? payload = await response.Content.ReadFromJsonAsync<ChatCompletionHttpResponse>(cancellationToken: cancellationToken);
        ChatCompletionHttpChoice? choice = payload?.Choices?.FirstOrDefault();
        if (choice?.Message?.Content is null)
        {
            _logger.LogWarning("LM Studio chat completion returned no assistant content for model {Model}", request.Model);
            throw new InvalidOperationException("LM Studio did not return assistant content.");
        }

        return new LmStudioChatCompletionResult(
            choice.Message.Content,
            payload?.Model ?? request.Model,
            choice.FinishReason ?? string.Empty);
    }

    private sealed record ChatCompletionHttpRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionHttpMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens);

    private sealed record ChatCompletionHttpMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionHttpResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionHttpChoice>? Choices);

    private sealed record ChatCompletionHttpChoice(
        [property: JsonPropertyName("finish_reason")] string? FinishReason,
        [property: JsonPropertyName("message")] ChatCompletionAssistantMessage? Message);

    private sealed record ChatCompletionAssistantMessage(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("content")] string? Content);
}
