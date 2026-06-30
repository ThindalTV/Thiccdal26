namespace Thiccdal.Infrastructure.Twitch;

public interface IEmoteRenderingOptions
{
    bool UseAnimatedEmotes { get; }
    void SetUseAnimatedEmotes(bool value);
    event EventHandler OptionsChanged;
}
