using System.Reflection;

namespace FriWorld.Launcher.Core;

/// <summary>
/// The launcher's own version, which is independent of the game's. The game moves on its
/// <c>bundleVersion</c>; the launcher moves when the launcher changes.
/// </summary>
public static class LauncherVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        // Source-linked builds append "+<commit>", which is noise in a window title.
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }

    /// <summary>
    /// True when the manifest names a launcher version other than the running one. No ordering,
    /// for the same reason the game's version is not ordered: the manifest is the authority.
    /// </summary>
    public static bool DiffersFrom(string? other) =>
        !string.IsNullOrWhiteSpace(other) &&
        !string.Equals(Current, other, StringComparison.Ordinal);
}
