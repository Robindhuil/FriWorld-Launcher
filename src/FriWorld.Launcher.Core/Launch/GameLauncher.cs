using System.Diagnostics;
using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Launch;

/// <summary>Resolves the game executable inside an install and starts it.</summary>
public sealed class GameLauncher(LauncherPaths paths, ILauncherLog? log = null)
{
    private readonly ILauncherLog _log = log ?? NullLauncherLog.Instance;

    /// <summary>
    /// Turns the manifest's <c>exec</c> value into an absolute path to a real binary.
    ///
    /// A macOS <c>.app</c> is a directory, not a program, so a manifest that names one cannot be
    /// started directly. Rather than fail, the real binary inside the bundle is resolved here —
    /// but the manifest should name it outright, because this guess relies on the usual layout.
    /// </summary>
    public string ResolveExecutable(string installDirectory, string exec)
    {
        if (string.IsNullOrWhiteSpace(exec))
        {
            throw new GameLaunchException("The manifest does not say which file to run.");
        }

        var path = Path.GetFullPath(Path.Combine(installDirectory, exec));

        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
        {
            var bundleName = Path.GetFileNameWithoutExtension(path);
            var inner = Path.Combine(path, "Contents", "MacOS", bundleName);

            if (File.Exists(inner))
            {
                _log.Warn($"The manifest points at a bundle; running {inner} instead.");
                return inner;
            }

            throw new GameLaunchException(
                $"'{exec}' is an app bundle and no executable was found at Contents/MacOS/{bundleName}. " +
                "The manifest should name the binary inside the bundle.");
        }

        if (!File.Exists(path))
        {
            throw new GameLaunchException($"The game executable is missing: {path}");
        }

        return path;
    }

    /// <summary>
    /// Makes sure the binary is executable. Needed when an archive lost its permission bits, which
    /// is exactly what happens when a Linux build is shipped as a zip.
    /// </summary>
    public static void EnsureExecutable(string executablePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(executablePath);
        var wanted = mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        if (mode != wanted)
        {
            File.SetUnixFileMode(executablePath, wanted);
        }
    }

    /// <summary>
    /// Where the list of running executables comes from.
    ///
    /// A seam, and it exists for one reason: a mock game is a shell script, so the process the
    /// operating system actually runs is the interpreter and nothing it reports lives under the
    /// install directory. The real scan can never see it, which makes every rule built on this
    /// untestable against a mock. A real Unity build is an executable in that directory and is
    /// seen normally.
    /// </summary>
    public Func<IEnumerable<string>> RunningExecutables { get; set; } = ScanRunningExecutables;

    /// <summary>
    /// Reports whether a process is already running out of the install directory.
    ///
    /// Two answers hang on this. Updating while the game runs would fail on Windows, where open
    /// files cannot be renamed away; and a second copy of the game would share one save directory
    /// with the first, where whichever exits last decides what the other's session was worth.
    /// </summary>
    public bool IsGameRunning()
    {
        if (!Directory.Exists(paths.Game))
        {
            return false;
        }

        var installRoot = Path.GetFullPath(paths.Game);

        foreach (var executablePath in RunningExecutables())
        {
            if (executablePath.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ScanRunningExecutables()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string? executablePath;

                try
                {
                    executablePath = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // System processes and processes owned by another user refuse inspection.
                    continue;
                }

                if (executablePath is not null)
                {
                    yield return executablePath;
                }
            }
        }
    }

    public Process Start(string executablePath, IEnumerable<string>? arguments = null)
    {
        EnsureExecutable(executablePath);

        var info = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
        };

        // Windows cannot execute a .cmd or .bat directly without a shell; the real game build is a
        // .exe, but the mock build is a script and the failure would otherwise look like a bug.
        if (OperatingSystem.IsWindows() && IsBatchScript(executablePath))
        {
            info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(executablePath);
        }

        foreach (var argument in arguments ?? [])
        {
            info.ArgumentList.Add(argument);
        }

        _log.Info($"Starting {executablePath}");

        return Process.Start(info)
            ?? throw new GameLaunchException($"The operating system did not start {executablePath}.");
    }

    private static bool IsBatchScript(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".cmd" or ".bat";
}

public sealed class GameLaunchException(string message) : Exception(message);
