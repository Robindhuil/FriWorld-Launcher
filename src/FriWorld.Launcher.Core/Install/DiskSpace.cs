using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Install;

/// <summary>
/// Free-space check, run before anything is downloaded.
/// </summary>
public static class DiskSpace
{
    /// <summary>
    /// How much room an update needs, as a multiple of the archive size.
    ///
    /// At peak the disk holds three copies of the build at once: the archive sitting in the cache,
    /// the freshly extracted tree in <c>game.new</c>, and the previous install still in
    /// <c>game.old</c>. For a build near a gigabyte that is the difference between "it worked" and
    /// running out of space halfway through an extract.
    /// </summary>
    public const double ArchiveSizeMultiplier = 3.0;

    private const long SafetyMargin = 512L * 1024 * 1024;

    public static long RequiredBytes(long archiveSize) =>
        (long)(archiveSize * ArchiveSizeMultiplier) + SafetyMargin;

    public static void Require(LauncherPaths paths, long archiveSize)
    {
        var required = RequiredBytes(archiveSize);

        long available;
        try
        {
            available = paths.Drive.AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Better to attempt the update than to refuse because the drive could not be queried.
            return;
        }

        if (available >= required)
        {
            return;
        }

        throw new InsufficientDiskSpaceException(
            $"Need about {Format(required)} free on {paths.Drive.Name} for a {Format(archiveSize)} " +
            $"download, but only {Format(available)} is available.");
    }

    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}

public sealed class InsufficientDiskSpaceException(string message) : Exception(message);
