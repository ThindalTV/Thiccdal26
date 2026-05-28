namespace Thiccdal.Remote.YouTube.Tests;

internal static class YouTubeTestData
{
    public static string CreatePollPayload(params string[] items)
    {
        return $$"""
        {
          "items": [
            {{string.Join(",\n            ", items)}}
          ]
        }
        """;
    }

    public static string CreateTextMessageItem(
        string messageId = "msg-1",
        string authorChannelId = "author-ch-1",
        string displayName = "TestUser",
        string messageText = "Hello YouTube!",
        string publishedAt = "2026-06-01T10:00:00Z")
    {
        return $$"""
        {
          "id": "{{messageId}}",
          "snippet": {
            "type": "textMessageEvent",
            "publishedAt": "{{publishedAt}}",
            "textMessageDetails": {
              "messageText": "{{messageText}}"
            }
          },
          "authorDetails": {
            "channelId": "{{authorChannelId}}",
            "displayName": "{{displayName}}"
          }
        }
        """;
    }

    public static string CreateSuperChatItem(
        string messageId = "sc-1",
        string displayName = "GeneroUser",
        long amountMicros = 5_000_000,
        string currency = "USD",
        string amountDisplayString = "$5.00",
        string? comment = "Great stream!",
        string publishedAt = "2026-06-01T10:05:00Z")
    {
        string commentProperty = comment is null
            ? string.Empty
            : $",\n              \"userComment\": \"{comment}\"";

        return $$"""
        {
          "id": "{{messageId}}",
          "snippet": {
            "type": "superChatEvent",
            "publishedAt": "{{publishedAt}}",
            "superChatDetails": {
              "amountMicros": {{amountMicros}},
              "currency": "{{currency}}",
              "amountDisplayString": "{{amountDisplayString}}"{{commentProperty}}
            }
          },
          "authorDetails": {
            "channelId": "donor-ch-1",
            "displayName": "{{displayName}}"
          }
        }
        """;
    }

    public static string CreateMembershipItem(
        string messageId = "milestone-1",
        string displayName = "MilestoneMember",
        int monthCount = 6,
        string levelName = "Gold",
        string publishedAt = "2026-06-01T10:10:00Z")
    {
        return $$"""
        {
          "id": "{{messageId}}",
          "snippet": {
            "type": "memberMilestoneChatEvent",
            "publishedAt": "{{publishedAt}}",
            "memberMilestoneChatDetails": {
              "memberMonth": {{monthCount}},
              "memberLevelName": "{{levelName}}"
            }
          },
          "authorDetails": {
            "channelId": "member-ch-1",
            "displayName": "{{displayName}}",
            "isChatSponsor": true
          }
        }
        """;
    }

    public static string CreateNewSponsorItem(
        string messageId = "sponsor-1",
        string displayName = "NewMember",
        string publishedAt = "2026-06-01T10:10:00Z")
    {
        return $$"""
        {
          "id": "{{messageId}}",
          "snippet": {
            "type": "newSponsorEvent",
            "publishedAt": "{{publishedAt}}"
          },
          "authorDetails": {
            "channelId": "sponsor-ch-1",
            "displayName": "{{displayName}}",
            "isChatSponsor": true
          }
        }
        """;
    }

    public static string CreateUnknownItem(
        string messageId = "unknown-1",
        string displayName = "UnknownUser",
        string type = "futureFeatureEvent",
        string publishedAt = "2026-06-01T10:15:00Z")
    {
        return $$"""
        {
          "id": "{{messageId}}",
          "snippet": {
            "type": "{{type}}",
            "publishedAt": "{{publishedAt}}"
          },
          "authorDetails": {
            "channelId": "unknown-ch-1",
            "displayName": "{{displayName}}"
          }
        }
        """;
    }
}
