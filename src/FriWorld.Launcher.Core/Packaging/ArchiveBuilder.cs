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

    /// <summary>
    /// Packs <paramref name="sourceDirectory"/> into <paramref name="archivePath"/>.
    ///
    /// The contents go in at the root, without a wrapping folder: the manifest's <c>exec</c> is
    /// relative to the install directory, so an extra top-level folder would make every path wrong.
    /// </summary>
    public static async Task CreateAsync(
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

        if (format == ArchiveFormat.Zip)
        {
            ZipFile.CreateFromDirectory(
                sourceDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return;
        }

        await CreateTarGzAsync(sourceDirectory, archivePath, executableRelativePath, ct).ConfigureAwait(false);
    }

    private static async Task CreateTarGzAsync(
        string sourceDirectory,
        string archivePath,
        string? executableRelativePath,
        CancellationToken ct)
    {
        var root = Path.GetFullPath(sourceDirectory);
        var executable = executableRelativePath is null ? null : Normalise(executableRelativePath);

        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        await using var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true);

        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();

            var relative = Normalise(Path.GetRelativePath(root, path));

            if (Directory.Exists(path))
            {
                await writer.WriteEntryAsync(
                    new PaxTarEntry(TarEntryType.Directory, relative)
                    {
                        Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                               UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                               UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                    },
                    ct).ConfigureAwait(false);
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
        }
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
