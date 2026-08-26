using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Sources;

/// <summary>
/// Where the launcher learns what the latest build is.
///
/// Everything behind this interface is swappable on purpose. Today it is a JSON file fetched from
/// a URL; whether that URL points at GitHub Releases, at object storage, or at a folder on this
/// machine is a configuration detail the rest of the launcher never sees.
/// </summary>
public interface IReleaseSource
{
    /// <summary>Human-readable description of where this source reads from, for logs and error messages.</summary>
    string Description { get; }

    Task<ReleaseManifest> GetLatestAsync(CancellationToken ct);
}
