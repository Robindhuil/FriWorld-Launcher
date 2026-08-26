namespace FriWorld.Launcher.Core.Manifest;

/// <summary>
/// What the launcher reads to decide whether to update. The shape is intentionally small because
/// the game's build pipeline regenerates it on every build; unknown fields are ignored on parse,
/// so the pipeline can add things without breaking older launchers.
/// </summary>
public sealed record ReleaseManifest
{
    /// <summary>The game's version tag. Compared for inequality only, never ordered.</summary>
    public string Version { get; init; } = string.Empty;

    public DateTimeOffset? Released { get; init; }

    /// <summary>Short release note shown in the launcher window.</summary>
    public string? Notes { get; init; }

    public IReadOnlyDictionary<string, PlatformPackage> Platforms { get; init; } =
        new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional. Present when a newer launcher exists. Absent manifests are entirely normal —
    /// the game's build pipeline does not have to know about launcher releases at all.
    /// </summary>
    public LauncherRelease? Launcher { get; init; }

    /// <summary>
    /// The oldest launcher allowed to act on this manifest. Optional, and normally absent.
    ///
    /// This is the escape hatch for the one problem tolerating unknown fields cannot solve. Adding
    /// a field is safe, because older launchers ignore it — but only while ignoring it still
    /// produces correct behaviour. The day a manifest means something an old launcher would get
    /// wrong, this field makes it stop and ask for an update instead of carrying on.
    ///
    /// Setting it locks out every launcher already in the wild below that version, so it is set
    /// only when the alternative is those launchers misbehaving.
    /// </summary>
    public string? MinLauncherVersion { get; init; }

    /// <summary>Returns the package for the first of <paramref name="platformKeys"/> the manifest carries.</summary>
    public bool TryGetPackage(IReadOnlyList<string> platformKeys, out string matchedKey, out PlatformPackage package)
    {
        foreach (var key in platformKeys)
        {
            if (Platforms.TryGetValue(key, out var found))
            {
                matchedKey = key;
                package = found;
                return true;
            }
        }

        matchedKey = string.Empty;
        package = new PlatformPackage();
        return false;
    }

    /// <summary>Throws if the manifest is structurally unusable. Called right after parsing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new ManifestException("Manifest has no version.");
        }

        if (Platforms.Count == 0)
        {
            throw new ManifestException("Manifest lists no platforms.");
        }

        foreach (var (key, package) in Platforms)
        {
            // Relative is allowed here: a manifest may name archives sitting beside it, and the
            // source resolves those against the manifest's own location straight after parsing.
            if (string.IsNullOrWhiteSpace(package.Url) ||
                !Uri.TryCreate(package.Url, UriKind.RelativeOrAbsolute, out _))
            {
                throw new ManifestException($"Platform '{key}' has no usable url.");
            }

            if (package.Sha256.Length != 64)
            {
                throw new ManifestException(
                    $"Platform '{key}' has a sha256 of {package.Sha256.Length} characters, expected 64.");
            }

            if (string.IsNullOrWhiteSpace(package.Exec))
            {
                throw new ManifestException($"Platform '{key}' has no exec path.");
            }

            if (package.Size <= 0)
            {
                throw new ManifestException($"Platform '{key}' has a size of {package.Size}.");
            }
        }
    }
}

public sealed class ManifestException(string message, Exception? inner = null)
    : Exception(message, inner);
