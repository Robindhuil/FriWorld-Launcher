using System.Globalization;
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
        string driveName;

        try
        {
            var drive = paths.Drive;
            available = drive.AvailableFreeSpace;
            driveName = drive.Name;
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

        // The message is English and goes in the log; the numbers beside it are what the window
        // shows, in whichever language it is speaking. A sentence built here could only ever be
        // one language, and the window would then have to show it in the other.
        throw new InsufficientDiskSpaceException(
            $"Need about {Format(required)} free on {driveName} for a {Format(archiveSize)} " +
            $"download, but only {Format(available)} is available.",
            required,
            available,
            driveName);
    }

    /// <summary>A size for a log or a developer's console, which are English everywhere.</summary>
    public static string Format(long bytes) => Format(bytes, CultureInfo.InvariantCulture.NumberFormat);

    /// <summary>
    /// A size for a person to read. <paramref name="format"/> carries the decimal separator,
    /// which is a comma in Slovak and a dot in English — the same number, and wrong in the other
    /// language either way.
    /// </summary>
    public static string Format(long bytes, NumberFormatInfo format)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString("0.##", format)} {units[unit]}";
    }
}

/// <summary>
/// Not enough room to download and unpack. Carries the numbers as numbers so the window can say
/// so in the player's language rather than repeating an English sentence built here.
/// </summary>
public sealed class InsufficientDiskSpaceException(
    string message,
    long requiredBytes = 0,
    long availableBytes = 0,
    string? driveName = null) : Exception(message)
{
    public long RequiredBytes { get; } = requiredBytes;

    public long AvailableBytes { get; } = availableBytes;

    public string? DriveName { get; } = driveName;
}
