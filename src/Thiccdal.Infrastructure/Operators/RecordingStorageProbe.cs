using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Resolves recording folder availability and drive free-space health from streaming options.
/// </summary>
public sealed class RecordingStorageProbe : IRecordingStorageProbe
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
    private const double MinimumFreeSpaceGb = 10d;

    private readonly IOptions<StreamingOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingStorageProbe"/> class.
    /// </summary>
    /// <param name="options">Provides the current ingest and recording output settings.</param>
    public RecordingStorageProbe(IOptions<StreamingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <inheritdoc />
    public RecordingStorageStatus GetStatus()
    {
        string configuredPath = (_options.Value.RecordingOutputPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new RecordingStorageStatus(
                false,
                "Set a recording output folder to enable local capture.",
                false,
                "Recording drive monitoring starts after a recording folder is configured.");
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(configuredPath);
            DirectoryInfo directory = Directory.CreateDirectory(fullPath);
            string? driveRoot = Path.GetPathRoot(directory.FullName);

            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return new RecordingStorageStatus(
                    false,
                    $"Recording output folder is invalid: {fullPath}",
                    false,
                    "Recording drive could not be determined from the configured output folder.");
            }

            DriveInfo drive = new(driveRoot);
            double freeSpaceGb = drive.AvailableFreeSpace / BytesPerGigabyte;

            return new RecordingStorageStatus(
                true,
                null,
                freeSpaceGb >= MinimumFreeSpaceGb,
                freeSpaceGb < MinimumFreeSpaceGb
                    ? $"Only {freeSpaceGb:F1} GB free on recording drive"
                    : null);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new RecordingStorageStatus(
                false,
                $"Recording output folder is unavailable: {ex.Message}",
                false,
                "Recording drive could not be checked until the output folder is available.");
        }
    }
}
