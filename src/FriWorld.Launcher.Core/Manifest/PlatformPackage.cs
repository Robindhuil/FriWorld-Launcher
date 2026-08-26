using System.Text.Json.Serialization;

namespace FriWorld.Launcher.Core.Manifest;

/// <summary>One downloadable build, for one platform key.</summary>
public sealed record PlatformPackage
{
    /// <summary>
    /// Where the archive is. Any scheme the content clients understand: https for real releases,
    /// file for local mocks. May also be relative, in which case it resolves against the manifest.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the archive. An archive that does not match is deleted, never extracted.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Archive size in bytes. Used for the free-space check and for progress before the server answers.</summary>
    public long Size { get; init; }

    /// <summary>
    /// Path of the executable inside the extracted tree, relative to the install root.
    ///
    /// This must point at a real binary, not at a directory. On macOS that means
    /// <c>FriWorld.app/Contents/MacOS/FriWorld</c> — a bare <c>FriWorld.app</c> is a folder
    /// and cannot be started directly.
    /// </summary>
    public string Exec { get; init; } = string.Empty;

    /// <summary>Optional. When absent the format is inferred from <see cref="Url"/>.</summary>
    public ArchiveFormat? Format { get; init; }

    // The two properties below are derived, so they are kept out of the JSON. Serialising them
    // would write fields the build pipeline never set and, worse, would evaluate them on a
    // manifest whose urls are still relative.

    [JsonIgnore]
    public ArchiveFormat ResolvedFormat => Format ?? ArchiveFormats.InferFrom(Url);

    /// <summary>File name to use in the download cache, derived from the URL.</summary>
    [JsonIgnore]
    public string CacheFileName
    {
        get
        {
            var path = Uri.TryCreate(Url, UriKind.Absolute, out var absolute)
                ? absolute.AbsolutePath
                : Url;

            var name = Path.GetFileName(path.Replace('\\', '/').TrimEnd('/'));
            return string.IsNullOrWhiteSpace(name) ? "download.bin" : name;
        }
    }
}
