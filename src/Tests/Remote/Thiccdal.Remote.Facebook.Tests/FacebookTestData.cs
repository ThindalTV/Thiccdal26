using System.Text.Json;
using Thiccdal.Infrastructure.Facebook;

namespace Thiccdal.Remote.Facebook.Tests;

public static class FacebookTestData
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static FacebookComment CreateComment(
        string id,
        string message,
        string userId,
        string displayName,
        string createdTime)
    {
        return new FacebookComment
        {
            Id = id,
            Message = message,
            From = new FacebookUser
            {
                Id = userId,
                Name = displayName
            },
            CreatedTime = createdTime
        };
    }

    public static FacebookReaction CreateReaction(string id, string type, string name)
    {
        return new FacebookReaction
        {
            Id = id,
            Type = type,
            Name = name
        };
    }

    public static string CommentsJson(params FacebookComment[] comments)
    {
        return JsonSerializer.Serialize(
            new FacebookPagedResponse<FacebookComment>
            {
                Data = comments
            },
            SerializerOptions);
    }

    public static string ReactionsJson(params FacebookReaction[] reactions)
    {
        return JsonSerializer.Serialize(
            new FacebookPagedResponse<FacebookReaction>
            {
                Data = reactions
            },
            SerializerOptions);
    }

    public static string LiveVideoJson(FacebookLiveVideo liveVideo)
    {
        return JsonSerializer.Serialize(liveVideo, SerializerOptions);
    }
}
