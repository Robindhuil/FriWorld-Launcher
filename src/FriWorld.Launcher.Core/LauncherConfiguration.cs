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
    /// Resolves configuration from, most specific first: the explicit argument, the environment,
    /// <c>launcher.json</c> beside the executable, then the built-in default.
    ///
    /// The argument comes first because a person typing a switch means it right now. The settings
    /// file comes last of the three because it belongs to the installation, not to this run — a
    /// development run must be able to point somewhere else without editing the deployed file.
    /// </summary>
    public static LauncherConfiguration Resolve(
        string? manifestUrlOverride = null,
        string? rootOverride = null,
        Action<string>? logMirror = null)
    {
        var settings = LauncherSettingsFile.Load();

        // A relative path typed on the command line means "from where I am standing", but the same
        // text in the settings file means "next to the launcher" — a shortcut can start it anywhere.
        var fromCommandLineOrEnvironment = Coalesce(
            manifestUrlOverride,
            Environment.GetEnvironmentVariable(ManifestUrlVariable));

        var url = fromCommandLineOrEnvironment is not null
            ? ParseUrlOrPath(fromCommandLineOrEnvironment)
            : settings.ManifestUrl is { } fromFile
                ? ParseUrlOrPath(ResolveAgainstExecutable(fromFile))
                : ParseUrlOrPath(DefaultManifestUrl);

        var root = Coalesce(
            rootOverride,
            Environment.GetEnvironmentVariable(LauncherPaths.RootOverrideVariable),
            settings.InstallRoot);
        var paths = root is null ? LauncherPaths.Default() : new LauncherPaths(ResolveAgainstExecutable(root));
        var log = new FileLauncherLog(paths.LogFile, logMirror);

        return new LauncherConfiguration(url, paths, log);
    }

    private static string? Coalesce(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>Resolves a relative path against the executable, leaving URLs and absolute paths alone.</summary>
    private static string ResolveAgainstExecutable(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out _) || Path.IsPathRooted(value)
            ? value
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, value));

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
