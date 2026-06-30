namespace Thiccdal.Infrastructure.Twitch;

public interface ITwitchStreamInfoService
{
    TwitchStreamState? CurrentState { get; }

    event EventHandler<TwitchStreamState?> StreamStateChanged;
}
