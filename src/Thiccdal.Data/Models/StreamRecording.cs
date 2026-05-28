namespace Thiccdal.Data.Models;

public sealed class StreamRecording
{
    public int Id { get; set; }

    public Guid? SessionId { get; set; }

    public string Platform { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string Error { get; set; } = string.Empty;
}
