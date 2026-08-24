namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// The single slot of content currently occupying the lower third overlay.
/// </summary>
/// <param name="Kind">What put the content on screen.</param>
/// <param name="Eyebrow">The small line above the body copy — a viewer name or a card category.</param>
/// <param name="Text">The body copy.</param>
/// <param name="Accent">The accent colour or platform key used for styling.</param>
/// <param name="StartedAt">When the content went on screen.</param>
/// <param name="QuestionId">The queued question behind the content, when the source is a question.</param>
public sealed record LowerThirdContent(
    LowerThirdContentKind Kind,
    string Eyebrow,
    string Text,
    string Accent,
    DateTimeOffset StartedAt,
    Guid? QuestionId);
