namespace Thiccdal.Infrastructure.Twitch;

public sealed class EmoteRenderingOptions : IEmoteRenderingOptions
{
    private bool _useAnimatedEmotes;

    public EmoteRenderingOptions(bool initialValue)
    {
        _useAnimatedEmotes = initialValue;
    }

    public bool UseAnimatedEmotes => _useAnimatedEmotes;

    public event EventHandler? OptionsChanged;

    public void SetUseAnimatedEmotes(bool value)
    {
        if (_useAnimatedEmotes == value) return;
        _useAnimatedEmotes = value;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
