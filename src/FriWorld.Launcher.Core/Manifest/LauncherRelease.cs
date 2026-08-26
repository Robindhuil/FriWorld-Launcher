namespace FriWorld.Launcher.Core.Manifest;

/// <summary>
/// Tells the launcher that a newer launcher exists, and where a person can get it.
///
/// This is deliberately not a self-update. Replacing a running executable is the fiddliest
/// part of the whole plan, it works differently on every operating system, and a bug in it
/// leaves the player with an installation that cannot repair itself. Since the launcher is a
/// stopgap until the game ships on a store, the cheap version wins: notice, and point at the
/// download page.
/// </summary>
public sealed record LauncherRelease
{
    /// <summary>Latest launcher version. Compared for inequality only, like the game's.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Page a person is sent to, not a file the launcher fetches.</summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>Optional one-liner about why it is worth updating.</summary>
    public string? Notes { get; init; }

    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(Version) &&
        Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var url) &&
        url.Scheme is "http" or "https";
}
