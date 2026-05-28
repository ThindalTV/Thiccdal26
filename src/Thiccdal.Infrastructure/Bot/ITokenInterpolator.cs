namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Replaces supported metadata tokens in chatbot response templates.
/// </summary>
public interface ITokenInterpolator
{
    /// <summary>
    /// Interpolates known metadata tokens in the supplied response template.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="context">The command invocation context.</param>
    /// <returns>The rendered response.</returns>
    string Interpolate(string template, CommandContext context);
}
