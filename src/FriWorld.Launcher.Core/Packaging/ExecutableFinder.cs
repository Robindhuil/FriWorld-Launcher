using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Packaging;

/// <summary>
/// Works out which file in a built player directory is the game.
///
/// Guessing is worth it because the answer is easy to get wrong by hand and the mistake only
/// shows up on a player's machine. Unity names the Linux binary <c>&lt;name&gt;.x86_64</c>, not
/// <c>&lt;name&gt;</c>, and on macOS the runnable file is buried inside the bundle rather than
/// being the bundle itself.
/// </summary>
public static class ExecutableFinder
{
    /// <summary>Names that look like the game but are not.</summary>
    private static readonly string[] NotTheGame =
    [
        "UnityCrashHandler64.exe",
        "UnityCrashHandler32.exe",
    ];

    public static string Find(string playerDirectory, string platformKey)
    {
        var found = TryFind(playerDirectory, platformKey);

        return found
            ?? throw new PackagingException(
                $"Could not tell which file to run in '{playerDirectory}' for {platformKey}. " +
                "Name it explicitly, for example --exec " + platformKey + "=FriWorld.exe");
    }

    public static string? TryFind(string playerDirectory, string platformKey)
    {
        if (!Directory.Exists(playerDirectory))
        {
            return null;
        }

        if (platformKey.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
        {
            return FindInBundle(playerDirectory);
        }

        if (platformKey == PlatformKey.WindowsX64)
        {
            return Single(playerDirectory, "*.exe");
        }

        // Unity's Linux player is <name>.x86_64; fall back to an extensionless file for a
        // build configured with a custom name.
        return Single(playerDirectory, "*.x86_64")
            ?? Single(playerDirectory, "*", extensionless: true);
    }

    private static string? FindInBundle(string playerDirectory)
    {
        var bundle = Directory
            .EnumerateDirectories(playerDirectory, "*.app", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (bundle is null)
        {
            return null;
        }

        var macOs = Path.Combine(bundle, "Contents", "MacOS");

        if (!Directory.Exists(macOs))
        {
            return null;
        }

        var binary = Directory.EnumerateFiles(macOs).FirstOrDefault();

        return binary is null
            ? null
            : Path.GetRelativePath(playerDirectory, binary).Replace('\\', '/');
    }

    private static string? Single(string directory, string pattern, bool extensionless = false)
    {
        var candidates = Directory
            .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Where(f => !NotTheGame.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !extensionless || string.IsNullOrEmpty(Path.GetExtension(f)))
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }
}

public sealed class PackagingException(string message) : Exception(message);
