namespace FriWorld.Launcher.Core.Install;

/// <summary>
/// What is currently on disk, as written to <c>installed.json</c>.
/// </summary>
public sealed record InstalledState
{
    /// <summary>The version tag this install came from.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>The platform key this install was built for.</summary>
    public string Platform { get; init; } = string.Empty;

    public DateTimeOffset InstalledAt { get; init; }

    /// <summary>Checksum of the archive it was extracted from, so a repair can tell what to re-fetch.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Executable path relative to the install directory, copied from the manifest.</summary>
    public string Exec { get; init; } = string.Empty;

    /// <summary>
    /// Set once the installed build has actually started at least once.
    ///
    /// Until it is set the previous install is kept, so a build that crashes on startup leaves a
    /// way back instead of leaving the player with a broken install and a launcher that cheerfully
    /// reports being up to date.
    /// </summary>
    public bool LaunchConfirmed { get; init; }
}
