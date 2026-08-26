using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Mock;
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
              mock-release             Generate a fake release in a local folder
              clean --yes              Delete the entire install root

            Options
              --manifest <url|path>    Manifest location. Overrides FRIWORLD_MANIFEST_URL.
              --root <path>            Install root. Overrides FRIWORLD_LAUNCHER_ROOT.
              --verbose                Mirror the log to stderr and print stack traces
              --wait                   run only: stay alive until the game exits

              --out <path>             mock-release only: output folder (default mock/store)
              --version <tag>          mock-release only: version to stamp
              --payload-mb <n>         mock-release only: filler size per platform (default 8)

            Exit codes
              0 ok · 1 error · 2 another launcher is running · 10 update available (check) · 130 cancelled

            Getting started
              dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
              dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
            """);

        return 0;
    }
}
