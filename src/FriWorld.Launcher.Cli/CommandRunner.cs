using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Packaging;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Verify;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Cli;

/// <summary>
/// A headless front end for the core.
///
/// The update pipeline is driven from here as well as from the window, which is the point: every
/// mechanic can be exercised and debugged without a UI in the way.
/// </summary>
public static class CommandRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = CommandLineOptions.Parse(args);

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
            Console.WriteLine();
            Console.WriteLine("Cancelling. A partial download is kept and will resume next time.");
        };

        try
        {
            return options.Command switch
            {
                "where" => Where(options),
                "pack" => await Pack(options, cancellation.Token),
                "mock-release" => await MockRelease(options, cancellation.Token),
                "check" => await Check(options, cancellation.Token),
                "update" => await Update(options, cancellation.Token),
                "run" => await Run(options, cancellation.Token),
                "play" => await Play(options, cancellation.Token),
                "repair" => await Repair(options, cancellation.Token),
                "uninstall" => Uninstall(options),
                "self-update" => await SelfUpdate(options, cancellation.Token),
                "clean" => Clean(options),
                "help" or "--help" or "-h" => Help(),
                _ => Unknown(options.Command),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(FailureMessages.Flatten(ex));

            if (options.Has("verbose"))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(ex);
            }

            return ex is GameIsRunningException ? 3 : ex is LauncherTooOldException ? 4 : 1;
        }
    }

    private static LauncherConfiguration Configure(CommandLineOptions options) =>
        LauncherConfiguration.Resolve(
            options.Value("manifest"),
            options.Value("root"),
            options.Has("verbose") ? Console.Error.WriteLine : null);

    private static int Where(CommandLineOptions options)
    {
        var config = Configure(options);
        var paths = config.Paths;

        Console.WriteLine($"launcher      {LauncherVersion.Current}");
        Console.WriteLine($"platform      {PlatformKey.Current}");
        Console.WriteLine($"manifest      {config.ManifestUrl}");
        Console.WriteLine($"root          {paths.Root}");
        Console.WriteLine($"  game        {paths.Game}");
        Console.WriteLine($"  game.new    {paths.GameNew}");
        Console.WriteLine($"  game.old    {paths.GameOld}");
        Console.WriteLine($"  cache       {paths.Cache}");
        Console.WriteLine($"  state       {paths.InstalledStateFile}");
        Console.WriteLine($"  log         {paths.LogFile}");

        var installed = new InstalledStateStore(paths).Read();
        Console.WriteLine();
        Console.WriteLine(installed is null
            ? "installed     nothing"
            : $"installed     {installed.Version} ({installed.Platform}), " +
              $"{(installed.LaunchConfirmed ? "confirmed" : "not yet launched")}");

        return 0;
    }

    /// <summary>
    /// Packs Unity's player output into a release. Run after the Unity build; this is the step
    /// that produces the archives, the checksums and the manifest the launcher reads.
    /// </summary>
    private static async Task<int> Pack(CommandLineOptions options, CancellationToken ct)
    {
        var input = options.Value("input")
            ?? throw new PackagingException("--input is required: the folder holding the platform subfolders.");
        var version = options.Value("version")
            ?? throw new PackagingException("--version is required: the game's bundleVersion.");

        var result = await ReleasePacker.PackAsync(
            new ReleasePacker.Options
            {
                InputDirectory = input,
                OutputDirectory = options.Value("out", Path.Combine("dist", version)),
                Version = version,
                Notes = options.Value("notes"),
                BaseUrl = options.Value("base-url"),
                ExecOverrides = ParseExecOverrides(options),
                Launcher = ParseLauncherRelease(options),
                MinLauncherVersion = options.Value("min-launcher"),
            },
            new Progress<string>(Console.WriteLine),
            ct);

        Console.WriteLine();
        Console.WriteLine($"Wrote {result.ManifestPath}");

        foreach (var platform in result.Platforms)
        {
            Console.WriteLine($"  {platform.PlatformKey,-12} {DiskSpace.Format(platform.Size),10}  {platform.Exec}");
        }

        Console.WriteLine();
        Console.WriteLine("Upload the archives and the manifest, then check it end to end with:");
        Console.WriteLine($"  launcher check --manifest \"{result.ManifestPath}\"");

        return 0;
    }

    /// <summary>Parses repeated <c>--exec &lt;platform&gt;=&lt;path&gt;</c> pairs.</summary>
    private static Dictionary<string, string> ParseExecOverrides(CommandLineOptions options)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in options.Values("exec"))
        {
            var split = pair.IndexOf('=');

            if (split <= 0)
            {
                throw new PackagingException($"--exec expects <platform>=<path>, got '{pair}'.");
            }

            overrides[pair[..split]] = pair[(split + 1)..];
        }

        return overrides;
    }

    /// <summary>
    /// Builds the manifest's launcher section. A download page alone is enough for the launcher
    /// to point someone at; adding a binary per platform is what lets it replace itself.
    /// </summary>
    private static LauncherRelease? ParseLauncherRelease(CommandLineOptions options)
    {
        var version = options.Value("launcher-version");
        var url = options.Value("launcher-url");
        var files = options.Values("launcher-file");

        if (version is null && url is null && files.Count == 0)
        {
            return null;
        }

        var release = new LauncherRelease
        {
            Version = version ?? LauncherVersion.Current,
            DownloadUrl = url ?? string.Empty,
            Notes = options.Value("launcher-notes"),
            Platforms = HashLauncherBinaries(files, options.Value("launcher-base-url")),
        };

        if (!release.IsUsable)
        {
            throw new PackagingException(
                "--launcher-url must be an absolute http or https address to a download page.");
        }

        return release;
    }

    /// <summary>Hashes each launcher binary and pairs it with the address it will be served from.</summary>
    private static Dictionary<string, LauncherBinary> HashLauncherBinaries(
        IReadOnlyList<string> files,
        string? baseUrl)
    {
        var binaries = new Dictionary<string, LauncherBinary>(StringComparer.OrdinalIgnoreCase);

        if (files.Count == 0)
        {
            return binaries;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new PackagingException(
                "--launcher-file needs --launcher-base-url, the https folder the binaries will be served from.");
        }

        foreach (var pair in files)
        {
            var split = pair.IndexOf('=');

            if (split <= 0)
            {
                throw new PackagingException($"--launcher-file expects <platform>=<path>, got '{pair}'.");
            }

            var platform = pair[..split];
            var path = pair[(split + 1)..];

            if (!File.Exists(path))
            {
                throw new PackagingException($"No such launcher binary: {path}");
            }

            var name = Path.GetFileName(path);
            var trimmed = baseUrl.TrimEnd('/');

            var binary = new LauncherBinary
            {
                Url = $"{trimmed}/{name}",
                Sha256 = Sha256Verifier.ComputeAsync(path).GetAwaiter().GetResult(),
                Size = new FileInfo(path).Length,
            };

            if (!binary.IsUsable)
            {
                throw new PackagingException(
                    $"The launcher binary for {platform} would be unusable. " +
                    "The base url must be https, because the launcher replaces itself with this file.");
            }

            binaries[platform] = binary;
            Console.WriteLine($"launcher {platform}: {DiskSpace.Format(binary.Size)}, sha256 {binary.Sha256[..16]}…");
        }

        return binaries;
    }

    private static async Task<int> MockRelease(CommandLineOptions options, CancellationToken ct)
    {
        var store = Path.GetFullPath(options.Value("out", Path.Combine("mock", "store")));
        var version = options.Value("version", $"0.0.1-mock.{DateTime.Now:MMddHHmm}");
        var payloadMb = int.Parse(options.Value("payload-mb", "8"));

        Console.WriteLine($"Building mock release {version} in {store}");

        var manifestPath = await MockReleaseBuilder.BuildAsync(
            store,
            new MockReleaseBuilder.Options
            {
                Version = version,
                PayloadBytes = payloadMb * 1024 * 1024,
                StubRunsForSeconds = int.Parse(options.Value("stub-seconds", "0")),
            },
            ct);

        Console.WriteLine();
        Console.WriteLine($"Wrote {manifestPath}");
        Console.WriteLine();
        Console.WriteLine("Point the launcher at it with:");
        Console.WriteLine($"  --manifest \"{manifestPath}\"");
        Console.WriteLine($"or set {LauncherConfiguration.ManifestUrlVariable} to the same path.");

        return 0;
    }

    private static async Task<int> Check(CommandLineOptions options, CancellationToken ct)
    {
        var orchestrator = Configure(options).CreateOrchestrator();
        var check = await orchestrator.CheckAsync(ct: ct);

        Console.WriteLine($"latest        {check.LatestVersion}");
        Console.WriteLine($"installed     {check.InstalledVersion ?? "nothing"}");
        Console.WriteLine($"platform      {check.PlatformKey}");
        Console.WriteLine($"archive       {DiskSpace.Format(check.Package.Size)} " +
                          $"({check.Package.ResolvedFormat})");
        Console.WriteLine($"exec          {check.Package.Exec}");

        if (!string.IsNullOrWhiteSpace(check.Manifest.Notes))
        {
            Console.WriteLine($"notes         {check.Manifest.Notes}");
        }

        Console.WriteLine();
        Console.WriteLine(check.UpdateRequired
            ? $"Update required: {check.Reason}."
            : "Up to date.");

        ReportLauncherUpdate(check);

        return check.UpdateRequired ? 10 : 0;
    }

    private static async Task<int> Update(CommandLineOptions options, CancellationToken ct)
    {
        var config = Configure(options);

        using var instanceLock = SingleInstanceLock.TryAcquire(config.Paths);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Another launcher is already running.");
            return 2;
        }

        var orchestrator = config.CreateOrchestrator();
        var check = await orchestrator.EnsureLatestAsync(new ConsoleProgressPrinter(), ct);

        Console.WriteLine(check.UpdateRequired
            ? $"Installed {check.LatestVersion}."
            : $"Nothing to do, {check.LatestVersion} is already installed.");

        ReportLauncherUpdate(check);

        return 0;
    }

    private static async Task<int> Run(CommandLineOptions options, CancellationToken ct)
    {
        var config = Configure(options);

        using var instanceLock = SingleInstanceLock.TryAcquire(config.Paths);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Another launcher is already running.");
            return 2;
        }

        var orchestrator = config.CreateOrchestrator();
        var printer = new ConsoleProgressPrinter();

        await orchestrator.EnsureLatestAsync(printer, ct);

        var process = await orchestrator.LaunchAsync(printer, ct: ct);
        Console.WriteLine($"Started process {process.Id}.");

        if (options.Has("wait"))
        {
            await process.WaitForExitAsync(ct);
            Console.WriteLine($"The game exited with code {process.ExitCode}.");
            return process.ExitCode;
        }

        return 0;
    }

    /// <summary>Starts what is installed without checking for anything. The "play the old one" path.</summary>
    private static async Task<int> Play(CommandLineOptions options, CancellationToken ct)
    {
        var config = Configure(options);

        using var instanceLock = SingleInstanceLock.TryAcquire(config.Paths);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Another launcher is already running.");
            return 2;
        }

        var orchestrator = config.CreateOrchestrator();
        var process = await orchestrator.LaunchAsync(new ConsoleProgressPrinter(), ct: ct);
        Console.WriteLine($"Started process {process.Id}.");

        if (!options.Has("wait"))
        {
            return 0;
        }

        await process.WaitForExitAsync(ct);
        Console.WriteLine($"The game exited with code {process.ExitCode}.");
        return process.ExitCode;
    }

    /// <summary>Reinstalls the current version over whatever is on disk.</summary>
    private static async Task<int> Repair(CommandLineOptions options, CancellationToken ct)
    {
        var config = Configure(options);

        using var instanceLock = SingleInstanceLock.TryAcquire(config.Paths);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Another launcher is already running.");
            return 2;
        }

        var installed = await config.CreateOrchestrator().RepairAsync(new ConsoleProgressPrinter(), ct);
        Console.WriteLine($"Reinstalled {installed.Version}.");
        return 0;
    }

    /// <summary>
    /// Replaces the launcher itself. Only possible for a single-file build; anything else is told
    /// to download manually, because a half-replaced launcher is worse than an old one.
    /// </summary>
    private static async Task<int> SelfUpdate(CommandLineOptions options, CancellationToken ct)
    {
        var config = Configure(options);
        var content = CompositeContentClient.CreateDefault();
        var updater = new LauncherSelfUpdater(content, config.Log);

        updater.CleanUpSupersededExecutable();

        var check = await config.CreateOrchestrator().CheckAsync(ct: ct);

        if (check.LauncherUpdate is not { } launcher)
        {
            Console.WriteLine($"Launcher {LauncherVersion.Current} is current.");
            return 0;
        }

        Console.WriteLine($"Launcher {launcher.Version} is available (this one is {LauncherVersion.Current}).");

        if (check.LauncherBinary is not { } binary)
        {
            Console.WriteLine($"The manifest carries no binary for {PlatformKey.Current}.");
            Console.WriteLine($"Download it from {launcher.DownloadUrl}");
            return 0;
        }

        if (updater.BlockedReason() is { } reason)
        {
            Console.WriteLine(reason);
            Console.WriteLine($"Download it from {launcher.DownloadUrl}");
            return 0;
        }

        if (!options.Has("yes"))
        {
            Console.WriteLine();
            Console.WriteLine("This replaces the running launcher. Re-run with --yes to go ahead.");
            return 0;
        }

        var staged = await updater.StageAsync(binary, new ConsoleDownloadPrinter(), ct);

        // Without --restart the new launcher is put in place and left for the next manual start,
        // which is what a script wants.
        updater.Apply(staged, restart: options.Has("restart"));

        Console.WriteLine();
        Console.WriteLine($"Launcher replaced with {launcher.Version}.");
        return 0;
    }

    /// <summary>
    /// Removes the game but leaves the log and the player's saves. `clean` deletes everything
    /// including the log; this is the one someone would actually want.
    /// </summary>
    private static int Uninstall(CommandLineOptions options)
    {
        var config = Configure(options);

        using var instanceLock = SingleInstanceLock.TryAcquire(config.Paths);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Another launcher is already running.");
            return 2;
        }

        var orchestrator = config.CreateOrchestrator();
        var installed = orchestrator.State.Read();

        if (installed is null)
        {
            Console.WriteLine("Nothing is installed.");
            return 0;
        }

        if (!options.Has("yes"))
        {
            Console.WriteLine($"This removes {installed.Version} from {config.Paths.Game}.");
            Console.WriteLine("Saves are kept; they live outside the install. Re-run with --yes.");
            return 1;
        }

        orchestrator.Uninstall();
        Console.WriteLine($"Removed {installed.Version}.");
        return 0;
    }

    private static int Clean(CommandLineOptions options)
    {
        var paths = Configure(options).Paths;

        if (!options.Has("yes"))
        {
            Console.Error.WriteLine($"This deletes {paths.Root}. Re-run with --yes to confirm.");
            return 1;
        }

        if (Directory.Exists(paths.Root))
        {
            Directory.Delete(paths.Root, recursive: true);
            Console.WriteLine($"Deleted {paths.Root}");
        }
        else
        {
            Console.WriteLine("Nothing to delete.");
        }

        return 0;
    }

    /// <summary>
    /// Mentions a newer launcher, without doing anything about it. Replacing a running executable
    /// is the most fragile part of a launcher and the game is heading for a store anyway, so the
    /// launcher never updates itself — it says so and points at the download page.
    /// </summary>
    private static void ReportLauncherUpdate(UpdateCheck check)
    {
        if (check.LauncherUpdate is not { } launcher)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"A newer launcher is available: {launcher.Version} " +
                          $"(this one is {LauncherVersion.Current}).");

        if (!string.IsNullOrWhiteSpace(launcher.Notes))
        {
            Console.WriteLine($"  {launcher.Notes}");
        }

        Console.WriteLine($"  {launcher.DownloadUrl}");
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Help();
        return 64;
    }

    private static int Help()
    {
        Console.WriteLine(
            """
            FriWorld launcher, headless front end.

            Commands
              where                    Show resolved paths, platform and install state
              check                    Fetch the manifest and report whether an update is needed
              update                   Download, verify, unpack and swap in the latest build
              run                      update, then start the game
              play                     Start what is installed, without checking for updates
              repair                   Reinstall the current version over a damaged one
              uninstall --yes          Remove the game, keeping the log and the player's saves
              self-update              Replace the launcher itself
              pack                     Turn Unity player output into a release plus manifest
              mock-release             Generate a fake release in a local folder
              clean --yes              Delete the entire install root, log included

            Options
              --manifest <url|path>    Manifest location. Overrides FRIWORLD_MANIFEST_URL.
              --root <path>            Install root. Overrides FRIWORLD_LAUNCHER_ROOT.
              --verbose                Mirror the log to stderr and print stack traces
              --wait                   run and play: stay alive until the game exits

            self-update
              --yes                    Actually do it; without this it only reports
              --restart                Start the new launcher afterwards

            pack
              --input <path>           Folder with one subfolder per platform key (required)
              --version <tag>          The game's bundleVersion (required)
              --out <path>             Output folder (default dist/<version>)
              --notes <text>           Release note shown in the launcher
              --base-url <url>         Prefix for archive urls; omit to write bare file names
              --exec <platform>=<path> Override the detected executable; may repeat
              --launcher-version <tag> Newest launcher, for the update notice
              --launcher-url <url>     Download page for the newest launcher
              --launcher-notes <text>  One-liner about the newest launcher
              --launcher-file <p>=<f>  Launcher binary per platform; enables self-update
              --launcher-base-url <u>  https folder the launcher binaries are served from
              --min-launcher <tag>     Refuse this release on older launchers

            mock-release
              --out <path>             Output folder (default mock/store)
              --version <tag>          Version to stamp
              --payload-mb <n>         Filler size per platform (default 8)

            Exit codes
              0 ok · 1 error · 2 another launcher is running · 3 the game is running
              4 launcher too old for this release · 10 update available (check) · 130 cancelled

            Getting started
              dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
              dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
            """);

        return 0;
    }
}
