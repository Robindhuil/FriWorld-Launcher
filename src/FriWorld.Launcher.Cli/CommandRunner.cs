using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Packaging;
using FriWorld.Launcher.Core.Platform;
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
            Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");

            if (options.Has("verbose"))
            {
                Console.Error.WriteLine(ex);
            }

            return 1;
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

    private static LauncherRelease? ParseLauncherRelease(CommandLineOptions options)
    {
        var version = options.Value("launcher-version");
        var url = options.Value("launcher-url");

        if (version is null && url is null)
        {
            return null;
        }

        var release = new LauncherRelease
        {
            Version = version ?? LauncherVersion.Current,
            DownloadUrl = url ?? string.Empty,
            Notes = options.Value("launcher-notes"),
        };

        if (!release.IsUsable)
        {
            throw new PackagingException(
                "--launcher-url must be an absolute http or https address to a download page.");
        }

        return release;
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
              pack                     Turn Unity player output into a release plus manifest
              mock-release             Generate a fake release in a local folder
              clean --yes              Delete the entire install root

            Options
              --manifest <url|path>    Manifest location. Overrides FRIWORLD_MANIFEST_URL.
              --root <path>            Install root. Overrides FRIWORLD_LAUNCHER_ROOT.
              --verbose                Mirror the log to stderr and print stack traces
              --wait                   run only: stay alive until the game exits

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

            mock-release
              --out <path>             Output folder (default mock/store)
              --version <tag>          Version to stamp
              --payload-mb <n>         Filler size per platform (default 8)

            Exit codes
              0 ok · 1 error · 2 another launcher is running · 10 update available (check) · 130 cancelled

            Getting started
              dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
              dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
            """);

        return 0;
    }
}
