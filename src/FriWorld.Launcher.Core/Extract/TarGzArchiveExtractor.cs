using System.Formats.Tar;
using System.IO.Compression;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Extract;

/// <summary>
/// Extracts gzipped tar archives, preserving unix file modes and symlinks.
///
/// This is the format for the Linux and macOS builds, and the reason is not taste. Tar records the
/// permission bits, so the game binary comes out executable; and it records symlinks as symlinks,
/// which a macOS <c>.app</c> bundle depends on. Zip does neither.
/// </summary>
public sealed class TarGzArchiveExtractor : IArchiveExtractor
{
    public ArchiveFormat Format => ArchiveFormat.TarGz;

    public async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        ExtractionPaths.PrepareDestination(destinationDirectory);

        // Compressed size is the only cheap total available, so progress tracks how much of the
        // archive has been consumed rather than how many bytes have been written.
        var compressedTotal = new FileInfo(archivePath).Length;
        var lastReported = 0d;

        await using var file = new FileStream(
            archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);

        while (await reader.GetNextEntryAsync(copyData: false, ct).ConfigureAwait(false) is { } entry)
        {
            ct.ThrowIfCancellationRequested();
            await ExtractEntry(entry, destinationDirectory, ct).ConfigureAwait(false);

            if (progress is not null && compressedTotal > 0)
            {
                var fraction = Math.Clamp((double)file.Position / compressedTotal, 0d, 1d);
                if (fraction - lastReported >= 0.01)
                {
                    lastReported = fraction;
                    progress.Report(fraction);
                }
            }
        }

        progress?.Report(1d);
    }

    private static async Task ExtractEntry(TarEntry entry, string destinationDirectory, CancellationToken ct)
    {
        var target = ExtractionPaths.ResolveInside(destinationDirectory, entry.Name);

        switch (entry.EntryType)
        {
            case TarEntryType.Directory:
                Directory.CreateDirectory(target);
                return;

            case TarEntryType.SymbolicLink:
            case TarEntryType.HardLink:
                ExtractLink(entry, target);
                return;

            case TarEntryType.RegularFile:
            case TarEntryType.V7RegularFile:
            case TarEntryType.ContiguousFile:
                await ExtractFile(entry, target, ct).ConfigureAwait(false);
                return;

            default:
                // Character devices, fifos and the like have no business in a game build.
                return;
        }
    }

    private static async Task ExtractFile(TarEntry entry, string target, CancellationToken ct)
    {
        ExtractionPaths.EnsureParentDirectory(target);

        await using (var destination = new FileStream(
            target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
        {
            if (entry.DataStream is { } data)
            {
                await data.CopyToAsync(destination, ct).ConfigureAwait(false);
            }
        }

        ApplyMode(target, entry.Mode);
    }

    private static void ExtractLink(TarEntry entry, string target)
    {
        if (string.IsNullOrEmpty(entry.LinkName))
        {
            return;
        }

        ExtractionPaths.EnsureParentDirectory(target);

        try
        {
            if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                File.CreateSymbolicLink(target, entry.LinkName);
            }
            else
            {
                var linkTarget = ExtractionPaths.ResolveInside(
                    Path.GetDirectoryName(target)!, entry.LinkName);
                File.Copy(linkTarget, target, overwrite: true);
            }
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            // Windows needs Developer Mode or elevation to create symlinks. Only mock archives are
            // ever unpacked here on Windows, so losing the link is not worth failing the install.
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
        }
    }

    private static void ApplyMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows() || mode == UnixFileMode.None)
        {
            return;
        }

        File.SetUnixFileMode(path, mode);
    }
}
