using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Update;

/// <summary>The answer to "is there anything to do?".</summary>
public sealed record UpdateCheck
{
    public required ReleaseManifest Manifest { get; init; }

    public required string PlatformKey { get; init; }

    public required PlatformPackage Package { get; init; }

    public required InstalledState? Installed { get; init; }

    public required UpdateReason Reason { get; init; }

    public bool UpdateRequired => Reason != UpdateReason.None;

    public string LatestVersion => Manifest.Version;

    public string? InstalledVersion => Installed?.Version;

    /// <summary>
    /// The newer launcher, when the manifest names one that is not the running version.
    /// Null otherwise. Nothing acts on this automatically — it exists to be shown to a person.
    /// </summary>
    public LauncherRelease? LauncherUpdate =>
        Manifest.Launcher is { IsUsable: true } launcher && LauncherVersion.DiffersFrom(launcher.Version)
            ? launcher
            : null;
}

/// <summary>
/// Why an update is needed.
///
/// Note what is missing: any notion of one version being newer than another. Ordering prerelease
/// tags correctly is fiddly and buys nothing, because the launcher only ever wants whatever the
/// manifest currently names. A tag that differs from the recorded one means install it.
/// </summary>
public enum UpdateReason
{
    None,
    NotInstalled,
    VersionDiffers,
    PlatformDiffers,
    InstallMissing,
}
