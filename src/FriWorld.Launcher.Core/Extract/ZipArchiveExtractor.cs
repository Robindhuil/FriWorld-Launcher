using System.IO.Compression;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Extract;

/// <summary>
/// Extracts zip archives, entry by entry so progress can be reported.
///
/// Zip is only ever used for the Windows build. It cannot carry a unix execute bit and it turns
/// symlinks into ordinary files, which is exactly how a Linux build ends up refusing to start and
/// a macOS <c>.app</c> bundle ends up broken.
/// </summary>
public sealed class ZipArchiveExtractor : IArchiveExtractor
{
    public ArchiveFormat Format => ArchiveFormat.Zip;

    public async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        ExtractionPaths.PrepareDestination(destinationDirectory);

        using var archive = ZipFile.OpenRead(archivePath);

        var totalBytes = archive.Entries.Sum(e => e.Length);
        long written = 0;
        var lastReported = 0d;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            var target = ExtractionPaths.ResolveInside(destinationDirectory, entry.FullName);

            // A directory entry has an empty name after the trailing separator.
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            ExtractionPaths.EnsureParentDirectory(target);

            await using (var source = entry.Open())
            await using (var destination = new FileStream(
                target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
            {
                await source.CopyToAsync(destination, ct).ConfigureAwait(false);
            }

            File.SetLastWriteTimeUtc(target, entry.LastWriteTime.UtcDateTime);

            written += entry.Length;

            if (progress is not null && totalBytes > 0)
            {
                var fraction = (double)written / totalBytes;
                if (fraction - lastReported >= 0.01)
                {
                    lastReported = fraction;
                    progress.Report(fraction);
                }
            }
        }

        progress?.Report(1d);
    }
}
