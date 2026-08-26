namespace FriWorld.Launcher.Core.Net;

/// <param name="BytesReceived">Total bytes on disk for this file, including anything a previous run left behind.</param>
/// <param name="TotalBytes">Full size of the file, or null when the source will not say.</param>
/// <param name="BytesPerSecond">Rolling estimate of throughput. Zero until enough samples exist.</param>
public readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes, double BytesPerSecond)
{
    public double? Fraction =>
        TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0d, 1d) : null;

    public TimeSpan? Remaining =>
        TotalBytes is > 0 && BytesPerSecond > 1
            ? TimeSpan.FromSeconds((TotalBytes.Value - BytesReceived) / BytesPerSecond)
            : null;
}
