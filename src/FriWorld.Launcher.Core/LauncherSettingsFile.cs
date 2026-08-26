using System.Text.Json;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core;

/// <summary>
/// Optional <c>launcher.json</c> sitting next to the executable.
///
/// Without it the manifest address would be baked into the binary, which means a different build
/// for every deployment and no way to move the release storage without shipping a new launcher to
/// everyone. A file beside the executable is edited in place instead.
/// </summary>
public sealed record LauncherSettingsFile
{
    public const string FileName = "launcher.json";

    /// <summary>Where the manifest lives. A URL, or a path for a local test.</summary>
    public string? ManifestUrl { get; init; }

    /// <summary>Where the game is installed. Normally absent, so the per-user default applies.</summary>
    public string? InstallRoot { get; init; }

    /// <summary>
    /// Reads the file beside the running executable. Returns an empty instance when it is absent
    /// or unreadable — a broken settings file must not stop the launcher from starting, because
    /// then there would be no way to tell the player what went wrong.
    /// </summary>
    public static LauncherSettingsFile Load(string? directory = null)
    {
        var folder = directory ?? ExecutableDirectory();

        if (folder is null)
        {
            return new LauncherSettingsFile();
        }

        var path = Path.Combine(folder, FileName);

        if (!File.Exists(path))
        {
            return new LauncherSettingsFile();
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherSettingsFile>(
                File.ReadAllText(path), ManifestJson.Options) ?? new LauncherSettingsFile();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new LauncherSettingsFile();
        }
    }

    /// <summary>
    /// The folder the launcher was started from.
    ///
    /// Deliberately <see cref="AppContext.BaseDirectory"/> and not the assembly location: a
    /// single-file build reports no location at all, because its assemblies are never on disk.
    /// </summary>
    private static string? ExecutableDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return string.IsNullOrWhiteSpace(baseDirectory) ? null : baseDirectory;
    }
}
