namespace FriWorld.Launcher.Core.Platform;

/// <summary>
/// Every path the launcher owns, resolved from a single root.
///
/// The root is deliberately per-user and never under Program Files or /usr — an install location
/// that needs elevation would prompt for admin rights on every single update.
/// </summary>
public sealed class LauncherPaths
{
    /// <summary>Set this to redirect the whole tree, which is how tests and dev runs avoid touching the real install.</summary>
    public const string RootOverrideVariable = "FRIWORLD_LAUNCHER_ROOT";

    private const string FolderName = "FriWorld";

    public LauncherPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    /// <summary>The live installation. This is what gets launched.</summary>
    public string Game => Path.Combine(Root, "game");

    /// <summary>Extraction target. A half-written install lives here and never touches <see cref="Game"/>.</summary>
    public string GameNew => Path.Combine(Root, "game.new");

    /// <summary>The previous install, kept until the new one has started successfully at least once.</summary>
    public string GameOld => Path.Combine(Root, "game.old");

    /// <summary>Downloaded archives, kept so an interrupted download can resume.</summary>
    public string Cache => Path.Combine(Root, "cache");

    /// <summary>The launcher's own binaries, relevant once self-update exists.</summary>
    public string LauncherDir => Path.Combine(Root, "launcher");

    public string InstalledStateFile => Path.Combine(Root, "installed.json");

    public string LogFile => Path.Combine(Root, "launcher.log");

    public string LockFile => Path.Combine(Root, "launcher.lock");

    /// <summary>Resolves the root from the override variable if set, otherwise the OS convention.</summary>
    public static LauncherPaths Default()
    {
        var overridden = Environment.GetEnvironmentVariable(RootOverrideVariable);
        return new LauncherPaths(string.IsNullOrWhiteSpace(overridden) ? ConventionalRoot() : overridden);
    }

    public static string ConventionalRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                FolderName);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", FolderName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".local", "share") : xdg;
        return Path.Combine(dataHome, FolderName);
    }

    /// <summary>Creates the directories that must exist before anything else runs.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(LauncherDir);
    }

    /// <summary>The drive the install lives on, used for the free-space check.</summary>
    public DriveInfo Drive => new(Path.GetPathRoot(Root) ?? Root);
}
