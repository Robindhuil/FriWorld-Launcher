using System.Diagnostics;

namespace FriWorld.Launcher.Core.Launch;

/// <summary>Opens a page in whatever browser the machine uses.</summary>
public static class SystemBrowser
{
    /// <summary>
    /// Opens <paramref name="url"/>, but only if it is http or https.
    ///
    /// The address comes from a manifest fetched over the network, so it is not trusted input.
    /// Handing an arbitrary scheme to the shell would let a manifest start a local program or
    /// open a file; restricting to web schemes keeps the worst case at "opens the wrong page".
    /// </summary>
    public static bool TryOpen(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        try
        {
            var info = OperatingSystem.IsWindows()
                ? new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }
                : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", uri.AbsoluteUri);

            using var process = Process.Start(info);
            return process is not null || OperatingSystem.IsWindows();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
