namespace Thiccdal.Infrastructure.Facebook;

public interface IFacebookGraphClient
{
    Task<FacebookLiveVideo> CreateLiveVideo(
        string pageId,
        string pageAccessToken,
        string title,
        string description,
        string privacy,
        CancellationToken cancellationToken = default);

    Task EndLiveVideo(
        string liveVideoId,
        string pageAccessToken,
        CancellationToken cancellationToken = default);

    Task<FacebookLiveVideo?> GetActiveLiveVideo(
        string pageId,
        string pageAccessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacebookComment>> GetComments(
        string liveVideoId,
        string pageAccessToken,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default);

    Task PostComment(
        string liveVideoId,
        string pageAccessToken,
        string message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacebookReaction>> GetReactions(
        string liveVideoId,
        string pageAccessToken,
        CancellationToken cancellationToken = default);

    Task UpdateLiveVideo(
        string liveVideoId,
        string pageAccessToken,
        string? title,
        string? description,
        CancellationToken cancellationToken = default);
}
