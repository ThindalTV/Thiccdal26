namespace Thiccdal.Infrastructure.Teleprompter;

public sealed class PrompterOptions
{
    public const string SectionName = "Prompter";

    public int ScrollStepPx { get; set; } = 150;
}
