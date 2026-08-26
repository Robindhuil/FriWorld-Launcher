using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Sources;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core;

/// <summary>
/// Assembles a working <see cref="UpdateOrchestrator"/> from the two things that vary between a
/// development run and a real one: where the manifest lives, and where the install goes.
/// </summary>
public sealed class LauncherConfiguration
{
    public const string ManifestUrlVariable = "FRIWORLD_MANIFEST_URL";

    /// <summary>
    /// Placeholder until the real storage exists.
    ///
    /// This is deliberately a plain static file rather than a release API. A static file has no
    /// rate limit — the GitHub API allows 60 unauthenticated calls an hour per address, which
    /// several players behind one connection can exhaust — and it is a layer of indirection, so
    /// moving the builds to different storage later means editing one JSON file instead of
    /// shipping a new launcher to everyone.
    /// </summary>
    public const string DefaultManifestUrl = "https://friworld.example/releases/manifest.json";

    public LauncherConfiguration(Uri manifestUrl, LauncherPaths paths, ILauncherLog log)
    {
        ManifestUrl = manifestUrl;
        Paths = paths;
        Log = log;
    }

    public Uri ManifestUrl { get; }

    public LauncherPaths Paths { get; }

    public ILauncherLog Log { get; }

    /// <summary>
    /// Resolves configuration from, in order: the explicit argument, the environment, the default.
    /// Both the manifest URL and the install root are overridable so a development run never
    /// touches the real installation.
    /// </summary>
    public static LauncherConfiguration Resolve(
        string? manifestUrlOverride = null,
        string? rootOverride = null,
        Action<string>? logMirror = null)
    {
        var raw = manifestUrlOverride
            ?? Environment.GetEnvironmentVariable(ManifestUrlVariable)
            ?? DefaultManifestUrl;

        var url = ParseUrlOrPath(raw);
        var paths = rootOverride is null ? LauncherPaths.Default() : new LauncherPaths(rootOverride);
        var log = new FileLauncherLog(paths.LogFile, logMirror);

        return new LauncherConfiguration(url, paths, log);
    }

    /// <summary>Accepts a URL or a local filesystem path, so a mock manifest can be named directly.</summary>
    public static Uri ParseUrlOrPath(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        return new Uri(Path.GetFullPath(value));
    }

    public UpdateOrchestrator CreateOrchestrator(IContentClient? content = null)
    {
        var client = content ?? CompositeContentClient.CreateDefault();
        var source = new JsonUrlReleaseSource(ManifestUrl, client);
        return new UpdateOrchestrator(Paths, source, client, Log);
    }
}
