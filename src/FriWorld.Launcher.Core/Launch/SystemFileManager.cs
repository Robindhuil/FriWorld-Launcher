using System.Diagnostics;

namespace FriWorld.Launcher.Core.Launch;

/// <summary>Opens the machine's file manager at the installed game.</summary>
public static class SystemFileManager
{
    /// <summary>
    /// Shows <paramref name="path"/> in the file manager, with the file itself selected where
    /// the platform can do that.
    ///
    /// Only ever called with a path the launcher itself computed, never with anything from a
    /// manifest — handing a downloaded string to the shell would be a different thing entirely.
    /// </summary>
    public static bool TryReveal(string path)
    {
        var full = Path.GetFullPath(path);
        var isFile = File.Exists(full);

        if (!isFile && !Directory.Exists(full))
        {
            return false;
        }

        try
        {
            var info = Info(full, isFile);
            using var process = Process.Start(info);

            // The Windows shell hands off to an existing explorer window and exits, so a null
            // process handle there means "already running", not "failed".
            return process is not null || OperatingSystem.IsWindows();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static ProcessStartInfo Info(string full, bool isFile)
    {
        if (OperatingSystem.IsWindows())
        {
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };

            // explorer wants /select and the path as one argument, and it is the one place
            // where ArgumentList quoting produces something it refuses to parse.
            info.Arguments = isFile ? $"/select,\"{full}\"" : $"\"{full}\"";
            return info;
        }

        if (OperatingSystem.IsMacOS())
        {
            var info = new ProcessStartInfo("open");

            if (isFile)
            {
                info.ArgumentList.Add("-R");
            }

            info.ArgumentList.Add(full);
            return info;
        }

        // Most Linux file managers cannot select a file from the command line, so the folder
        // holding it is the closest honest equivalent.
        var folder = isFile ? Path.GetDirectoryName(full)! : full;
        var linux = new ProcessStartInfo("xdg-open");
        linux.ArgumentList.Add(folder);
        return linux;
    }
}
