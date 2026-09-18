using FriWorld.Launcher.Core.Localization;

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

    /// <summary>Short release note shown in the launcher window. Written in Slovak, like the window.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// The same note in English. Optional, and absent on every manifest written before the window
    /// spoke English at all.
    ///
    /// A second field rather than turning <c>notes</c> into an object keyed by language: a
    /// manifest where <c>notes</c> stopped being a string would fail to parse in every launcher
    /// already on a school computer, and a launcher that cannot read the manifest cannot update
    /// itself out of the problem either. An added field is ignored by those launchers, which is
    /// exactly the right outcome — they only ever showed Slovak.
    /// </summary>
    public string? NotesEn { get; init; }

    /// <summary>
    /// The note to show, falling back to Slovak when the release carries no English one.
    ///
    /// Falling back is deliberate. The note is the one place a human writes prose into the
    /// manifest, and an English window with an empty "What's new" says less than an English
    /// window with a Slovak sentence in it.
    /// </summary>
    public string? NotesFor(Language language) =>
        language == Language.English ? NotesEn ?? Notes : Notes;

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
