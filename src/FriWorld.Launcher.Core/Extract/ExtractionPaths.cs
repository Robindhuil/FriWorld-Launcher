namespace FriWorld.Launcher.Core.Extract;

internal static class ExtractionPaths
{
    /// <summary>
    /// Resolves an entry name against the destination and refuses anything that escapes it.
    ///
    /// An archive entry called <c>../../autoexec</c> is a real attack and a real accident, and the
    /// launcher extracts archives it fetched over the network, so the check is not optional.
    /// </summary>
    public static string ResolveInside(string destinationDirectory, string entryName)
    {
        var root = Path.GetFullPath(destinationDirectory);
        var normalised = entryName.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(root, normalised));

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ExtractionException(
                $"Archive entry '{entryName}' would be written outside the destination directory.");
        }

        return full;
    }

    public static void EnsureParentDirectory(string filePath)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    /// <summary>Empties the destination so a previous failed attempt cannot leave stale files behind.</summary>
    public static void PrepareDestination(string destinationDirectory)
    {
        if (Directory.Exists(destinationDirectory))
        {
            Directory.Delete(destinationDirectory, recursive: true);
        }

        Directory.CreateDirectory(destinationDirectory);
    }
}
