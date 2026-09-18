using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Localization;
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

    /// <summary>Forces the language for one run, the way the manifest URL can be forced.</summary>
    public const string LanguageVariable = "FRIWORLD_LANGUAGE";

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

    public LauncherConfiguration(
        Uri manifestUrl,
        LauncherPaths paths,
        ILauncherLog log,
        Language language = Languages.Default)
    {
        ManifestUrl = manifestUrl;
        Paths = paths;
        Log = log;
        Language = language;
    }

    public Uri ManifestUrl { get; }

    public LauncherPaths Paths { get; }

    public ILauncherLog Log { get; }

    /// <summary>The language this run speaks to a player in.</summary>
    public Language Language { get; }

    /// <summary>Everything the window says, in <see cref="Language"/>.</summary>
    public Texts Texts => Texts.For(Language);

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
        Action<string>? logMirror = null,
        string? languageOverride = null)
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

        var language = ResolveLanguage(
            languageOverride,
            Environment.GetEnvironmentVariable(LanguageVariable),
            LanguagePreference.Read(paths),
            settings.Language);

        return new LauncherConfiguration(url, paths, log, language);
    }

    /// <summary>
    /// Picks the language, most specific first: the explicit argument, the environment, the
    /// choice someone made in the window, then <c>launcher.json</c>, then Slovak.
    ///
    /// The remembered choice beats the settings file on purpose. The file says what a deployment
    /// starts in; the switch in the window is a person saying what they want, and a preference
    /// that a restart quietly undid would not be worth offering.
    ///
    /// There is deliberately no guess at the system language. The whole solution builds with
    /// <c>InvariantGlobalization</c>, so the culture APIs would answer "invariant" on a Slovak
    /// Windows as readily as on an English one — and the machines this runs on are school
    /// computers, which are Slovak. Guessing badly here would mean a Slovak child opening an
    /// English window, which is worse than not guessing at all.
    /// </summary>
    internal static Language ResolveLanguage(
        string? languageOverride,
        string? fromEnvironment,
        Language? remembered,
        string? fromSettingsFile) =>
        Languages.TryParse(languageOverride)
        ?? Languages.TryParse(fromEnvironment)
        ?? remembered
        ?? Languages.TryParse(fromSettingsFile)
        ?? Languages.Default;

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
