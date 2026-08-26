using System.Text.Json;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Install;

/// <summary>Reads and writes <c>installed.json</c>.</summary>
public sealed class InstalledStateStore(LauncherPaths paths)
{
    /// <summary>
    /// Returns null when nothing is installed, and also when the file is unreadable. A corrupt
    /// state file should mean "reinstall", not "crash on startup".
    /// </summary>
    public InstalledState? Read()
    {
        if (!File.Exists(paths.InstalledStateFile))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(paths.InstalledStateFile);
            return JsonSerializer.Deserialize<InstalledState>(json, ManifestJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public void Write(InstalledState state)
    {
        Directory.CreateDirectory(paths.Root);

        // Write beside the target and move into place, so an interrupted write cannot leave a
        // half-written state file that the next run would treat as "nothing installed".
        var temporary = paths.InstalledStateFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, ManifestJson.Options));
        File.Move(temporary, paths.InstalledStateFile, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(paths.InstalledStateFile))
        {
            File.Delete(paths.InstalledStateFile);
        }
    }
}
