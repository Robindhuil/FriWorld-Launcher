using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Extract;

public interface IArchiveExtractor
{
    ArchiveFormat Format { get; }

    /// <summary>
    /// Extracts into <paramref name="destinationDirectory"/>, which is created empty first.
    /// Progress is reported as a fraction between 0 and 1.
    /// </summary>
    Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<double>? progress,
        CancellationToken ct);
}

public static class ArchiveExtractors
{
    public static IArchiveExtractor For(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => new ZipArchiveExtractor(),
        ArchiveFormat.TarGz => new TarGzArchiveExtractor(),
        _ => throw new NotSupportedException($"No extractor for {format}."),
    };
}

public sealed class ExtractionException(string message, Exception? inner = null)
    : Exception(message, inner);
