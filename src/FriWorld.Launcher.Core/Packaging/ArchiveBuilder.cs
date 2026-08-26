using System.Formats.Tar;
using System.IO.Compression;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Packaging;

/// <summary>
/// Turns a built player directory into a release archive.
///
/// This lives in the launcher rather than in the game's build script for two reasons. Unity runs
/// on an older framework that has no tar writer at all. And a tar written on Windows records no
/// execute bit, because a Windows filesystem has none to record — so the mode has to be set
/// deliberately on the way in, which is exactly what <paramref name="executableRelativePath"/> is for.
/// </summary>
public static class ArchiveBuilder
{
    private const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode RegularMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// Folders Unity emits beside the player and names, in the folder name itself, as things not
    /// to ship. They hold debug symbols and they embed absolute paths from the build machine.
    /// Unity puts the warning in the name because the build pipeline will not remove them for you.
    /// </summary>
    private static readonly string[] DoNotShipSuffixes =
    [
        "_BurstDebugInformation_DoNotShip",
        "_BackUpThisFolder_ButDontShipItWithYourGame",
    ];

    /// <summary>
    /// Packs <paramref name="sourceDirectory"/> into <paramref name="archivePath"/>.
    ///
    /// The contents go in at the root, without a wrapping folder: the manifest's <c>exec</c> is
    /// relative to the install directory, so an extra top-level folder would make every path wrong.
    /// </summary>
    public static async Task<int> CreateAsync(
        string sourceDirectory,
        string archivePath,
        ArchiveFormat format,
        string? executableRelativePath = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var root = Path.GetFullPath(sourceDirectory);
        var executable = executableRelativePath is null ? null : Normalise(executableRelativePath);

        return format == ArchiveFormat.Zip
            ? await CreateZipAsync(root, archivePath, ct).ConfigureAwait(false)
            : await CreateTarGzAsync(root, archivePath, executable, ct).ConfigureAwait(false);
    }

    /// <summary>Names of the entries that would be skipped, for reporting before a pack runs.</summary>
    public static IReadOnlyList<string> ExcludedEntries(string sourceDirectory)
    {
        var root = Path.GetFullPath(sourceDirectory);

        return Directory
            .EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly)
            .Select(path => Normalise(Path.GetRelativePath(root, path)))
            .Where(IsExcluded)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static async Task<int> CreateZipAsync(string root, string archivePath, CancellationToken ct)
    {
        // Written entry by entry rather than with CreateFromDirectory, because that has no way to
        // leave anything out and the do-not-ship folders have to be left out.
        await using var file = File.Create(archivePath);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        var written = 0;

        foreach (var (path, relative) in Entries(root))
        {
            ct.ThrowIfCancellationRequested();

            if (Directory.Exists(path))
            {
                continue;
            }

            var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
            entry.LastWriteTime = File.GetLastWriteTime(path);

            await using var source = File.OpenRead(path);
            await using var destination = entry.Open();
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);

            written++;
        }

        return written;
    }

    private static async Task<int> CreateTarGzAsync(
        string root,
        string archivePath,
        string? executable,
        CancellationToken ct)
    {
        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        await using var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true);

        var written = 0;

        foreach (var (path, relative) in Entries(root))
        {
            ct.ThrowIfCancellationRequested();

            if (Directory.Exists(path))
            {
                await writer
                    .WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, relative) { Mode = DirectoryMode }, ct)
                    .ConfigureAwait(false);
                continue;
            }

            var entry = new PaxTarEntry(TarEntryType.RegularFile, relative)
            {
                Mode = ShouldBeExecutable(relative, executable) ? ExecutableMode : ModeOf(path),
            };

            await using (var content = File.OpenRead(path))
            {
                entry.DataStream = content;
                await writer.WriteEntryAsync(entry, ct).ConfigureAwait(false);
            }

            written++;
        }

        return written;
    }

    private static IEnumerable<(string Path, string Relative)> Entries(string root) => Directory
        .EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(path => (Path: path, Relative: Normalise(Path.GetRelativePath(root, path))))
        .Where(entry => !IsExcluded(entry.Relative));

    private static bool IsExcluded(string relative)
    {
        foreach (var segment in relative.Split('/'))
        {
            foreach (var suffix in DoNotShipSuffixes)
            {
                if (segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Decides which files come out runnable. The named executable always does; so does anything
    /// inside a macOS bundle's <c>Contents/MacOS</c>, and Unity's crash handler, which the game
    /// spawns itself and which fails silently when it cannot be run.
    /// </summary>
    private static bool ShouldBeExecutable(string relative, string? executable)
    {
        if (executable is not null && relative.Equals(executable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (relative.Contains("/Contents/MacOS/", StringComparison.Ordinal))
        {
            return true;
        }

        return relative.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith("UnityCrashHandler64", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the mode from disk when the platform has one. On Windows there is nothing to read,
    /// so a sane default is used instead of whatever .NET would invent.
    /// </summary>
    private static UnixFileMode ModeOf(string path) =>
        OperatingSystem.IsWindows() ? RegularMode : File.GetUnixFileMode(path);

    private static string Normalise(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
