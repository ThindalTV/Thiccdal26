using System.Text.Json;
using Microsoft.Extensions.Logging;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;

namespace Thiccdal.Data;

internal static class PlatformUserIdResolver
{
    public static string Resolve(RuntimeChatEvent chatEvent, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(chatEvent.RawData))
        {
            return chatEvent.Author;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(chatEvent.RawData);
            JsonElement root = document.RootElement;
            if (TryResolve(root, out string platformUserId))
            {
                return platformUserId;
            }
        }
        catch (JsonException exception)
        {
            logger.LogDebug(exception, "Could not resolve platform user id from raw event payload.");
        }

        return chatEvent.Author;
    }

    private static bool TryResolve(JsonElement root, out string platformUserId)
    {
        platformUserId = string.Empty;

        if (TryResolveYouTubeAuthor(root, out platformUserId))
        {
            return true;
        }

        if (TryResolveFacebookAuthor(root, out platformUserId))
        {
            return true;
        }

        if (!root.TryGetProperty("payload", out JsonElement payloadElement) ||
            !payloadElement.TryGetProperty("event", out JsonElement eventElement))
        {
            return false;
        }

        if (!TryGetString(eventElement, "chatter_user_id", out platformUserId) &&
            !TryGetString(eventElement, "user_id", out platformUserId))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(platformUserId);
    }

    private static bool TryResolveYouTubeAuthor(JsonElement root, out string platformUserId)
    {
        platformUserId = string.Empty;

        if (!root.TryGetProperty("authorDetails", out JsonElement authorDetails))
        {
            return false;
        }

        return TryGetString(authorDetails, "channelId", out platformUserId);
    }

    private static bool TryResolveFacebookAuthor(JsonElement root, out string platformUserId)
    {
        platformUserId = string.Empty;

        if (!root.TryGetProperty("from", out JsonElement from))
        {
            return false;
        }

        return TryGetString(from, "id", out platformUserId);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out JsonElement propertyElement) ||
            propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
