using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Net;

namespace FriWorld.Launcher.Core.Sources;

/// <summary>
/// Reads the manifest as a plain JSON document at a fixed URL.
///
/// This one implementation covers every case that matters. Pointed at a static file on the web it
/// is the production source, and unlike the GitHub API it has no unauthenticated rate limit, which
/// matters because several users behind one NAT share an address. Pointed at a <c>file://</c> URL
/// it is the local mock. Because the URL is fixed rather than derived from a release API, moving
/// the build to different storage later means editing one JSON file, not shipping a new launcher.
/// </summary>
public sealed class JsonUrlReleaseSource(Uri manifestUrl, IContentClient content) : IReleaseSource
{
    public Uri ManifestUrl { get; } = manifestUrl;

    public string Description => ManifestUrl.ToString();

    public async Task<ReleaseManifest> GetLatestAsync(CancellationToken ct)
    {
        var json = await content.GetStringAsync(ManifestUrl, ct).ConfigureAwait(false);
        var manifest = ManifestJson.Parse(json);
        return ResolveRelativeUrls(manifest);
    }

    /// <summary>
    /// Lets a manifest reference archives sitting next to it by file name instead of by absolute
    /// URL. Without this the mock manifest would have to be rewritten whenever the folder moves.
    /// </summary>
    private ReleaseManifest ResolveRelativeUrls(ReleaseManifest manifest)
    {
        var resolved = new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, package) in manifest.Platforms)
        {
            if (Uri.TryCreate(package.Url, UriKind.Absolute, out _))
            {
                resolved[key] = package;
                continue;
            }

            if (!Uri.TryCreate(ManifestUrl, package.Url, out var absolute))
            {
                throw new ManifestException(
                    $"Platform '{key}' has url '{package.Url}', which does not resolve against {ManifestUrl}.");
            }

            resolved[key] = package with { Url = absolute.ToString() };
        }

        return manifest with { Platforms = resolved };
    }
}
