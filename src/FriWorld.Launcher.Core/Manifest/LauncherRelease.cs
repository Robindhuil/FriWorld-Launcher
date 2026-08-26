using System.Text.Json.Serialization;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Manifest;

/// <summary>
/// Tells the launcher that a newer launcher exists.
///
/// Two levels are supported on purpose. <see cref="DownloadUrl"/> alone is a page a person is
/// sent to, and always works. <see cref="Platforms"/> additionally carries the binary itself, and
/// only then can the launcher replace itself without help.
/// </summary>
public sealed record LauncherRelease
{
    /// <summary>Latest launcher version. Compared for inequality only, like the game's.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Page a person is sent to when the launcher cannot replace itself.</summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>Optional one-liner about why it is worth updating.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// The launcher binary per platform key. Optional: without it the update is a link, with it
    /// the launcher can fetch and swap itself.
    /// </summary>
    public IReadOnlyDictionary<string, LauncherBinary> Platforms { get; init; } =
        new Dictionary<string, LauncherBinary>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether there is at least a page to send someone to.</summary>
    [JsonIgnore]
    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(Version) &&
        Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var url) &&
        url.Scheme is "http" or "https";

    /// <summary>The binary for this machine, when the manifest carries one that is usable.</summary>
    [JsonIgnore]
    public LauncherBinary? BinaryForThisPlatform
    {
        get
        {
            foreach (var key in PlatformKey.CurrentWithFallbacks)
            {
                if (Platforms.TryGetValue(key, out var binary) && binary.IsUsable)
                {
                    return binary;
                }
            }

            return null;
        }
    }
}

/// <summary>One downloadable launcher executable.</summary>
public sealed record LauncherBinary
{
    public string Url { get; init; } = string.Empty;

    /// <summary>Lowercase hex SHA-256. Nothing is swapped in without matching this.</summary>
    public string Sha256 { get; init; } = string.Empty;

    public long Size { get; init; }

    /// <summary>
    /// Only an https binary is ever fetched for a self-update.
    ///
    /// The game archive is checksummed against a manifest, so plain http would still be caught;
    /// but the launcher replaces itself with this file, so it gets the stricter rule. A manifest
    /// served over a hijacked connection must not be able to hand over an executable.
    /// </summary>
    [JsonIgnore]
    public bool IsUsable =>
        Sha256.Length == 64 &&
        Size > 0 &&
        Uri.TryCreate(Url, UriKind.Absolute, out var url) &&
        url.Scheme == "https";
}
