using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Data collected from the streaming configuration step.
/// </summary>
public sealed record StreamingStepData(
    StreamingDeploymentMode DeploymentMode,
    string IngestUrl,
    string ExternalHost,
    int ExternalApiPort,
    string ApiKey,
    string RecordingPath,
    string FfmpegPath,
    string BrbSlatePath,
    bool StartWithHost);
