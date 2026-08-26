using System.Globalization;
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
    /// True when the manifest names a launcher version other than the running one.
    ///
    /// No ordering, for the same reason the game's version is not ordered: whatever the manifest
    /// currently names is what should be running, even if the number went down.
    /// </summary>
    public static bool DiffersFrom(string? other) =>
        !string.IsNullOrWhiteSpace(other) &&
        !string.Equals(Current, other, StringComparison.Ordinal);

    /// <summary>
    /// True when the running launcher is older than <paramref name="minimum"/>.
    ///
    /// This is the one place in the launcher where versions are ordered rather than merely
    /// compared, and it exists for one reason: a manifest may one day need a field that older
    /// launchers cannot act on. Tolerating unknown fields is not enough there — the old launcher
    /// would carry on and quietly do the wrong thing. With a floor it can stop and say so.
    ///
    /// An unparseable or absent minimum is treated as no minimum. A gate that fires by accident
    /// would lock people out of a game that would have worked.
    /// </summary>
    public static bool IsOlderThan(string? minimum) =>
        SemanticVersion.TryParse(minimum, out var floor) &&
        SemanticVersion.TryParse(Current, out var current) &&
        current.CompareTo(floor) < 0;
}

/// <summary>
/// Enough of semantic versioning to answer "is this one older than that one".
///
/// Deliberately small and forgiving: it exists only for the launcher's own minimum-version gate,
/// where the alternative is a NuGet dependency for one comparison.
/// </summary>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string PreRelease)
    : IComparable<SemanticVersion>
{
    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();

        // Build metadata never affects precedence.
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        var preRelease = string.Empty;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = value[(dash + 1)..];
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length == 0 || parts.Length > 3)
        {
            return false;
        }

        var numbers = new int[3];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], preRelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
        {
            return minor;
        }

        var patch = Patch.CompareTo(other.Patch);
        if (patch != 0)
        {
            return patch;
        }

        // 1.0.0-alpha precedes 1.0.0. Two prereleases are compared as plain text, which is right
        // for alpha/beta/rc and close enough for anything else this project will produce.
        var mine = string.IsNullOrEmpty(PreRelease);
        var theirs = string.IsNullOrEmpty(other.PreRelease);

        if (mine && theirs)
        {
            return 0;
        }

        if (mine != theirs)
        {
            return mine ? 1 : -1;
        }

        return string.CompareOrdinal(PreRelease, other.PreRelease);
    }
}
