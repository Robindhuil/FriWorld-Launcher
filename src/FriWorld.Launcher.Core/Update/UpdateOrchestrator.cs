using System.Diagnostics;
using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Extract;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Sources;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Update;

/// <summary>
/// The whole update path in one place: check, download, verify, extract, swap, launch.
///
/// Nothing in here knows where the build comes from. It talks to an <see cref="IReleaseSource"/>
/// and an <see cref="IContentClient"/>, which is what lets the same code run against a folder on
/// this machine during development and against real remote storage in production.
/// </summary>
public sealed class UpdateOrchestrator(
    LauncherPaths paths,
    IReleaseSource source,
    IContentClient content,
    ILauncherLog? log = null)
{
    private readonly ILauncherLog _log = log ?? NullLauncherLog.Instance;
    private readonly InstalledStateStore _state = new(paths);
    private readonly AtomicInstaller _installer = new(paths, log);
    private readonly GameLauncher _launcher = new(paths, log);

    public LauncherPaths Paths => paths;

    public InstalledStateStore State => _state;

    public GameLauncher Launcher => _launcher;

    public async Task<UpdateCheck> CheckAsync(
        IProgress<UpdateStatus>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(UpdateStatus.Of(UpdateStage.CheckingForUpdate, "Kontrolujem aktualizácie"));
        _log.Info($"Reading the manifest from {source.Description}");

        var manifest = await source.GetLatestAsync(ct).ConfigureAwait(false);

        if (!manifest.TryGetPackage(PlatformKey.CurrentWithFallbacks, out var key, out var package))
        {
            throw new UpdateException(
                $"Release {manifest.Version} has no build for {PlatformKey.Current}. " +
                $"It offers: {string.Join(", ", manifest.Platforms.Keys)}.");
        }

        if (LauncherVersion.IsOlderThan(manifest.MinLauncherVersion))
        {
            _log.Warn(
                $"This manifest needs launcher {manifest.MinLauncherVersion} or newer; " +
                $"this one is {LauncherVersion.Current}.");
        }

        var installed = _state.Read();
        var reason = DecideReason(installed, manifest.Version, key);

        _log.Info(reason == UpdateReason.None
            ? $"Up to date on {manifest.Version} ({key})."
            : $"Update needed ({reason}): installed {installed?.Version ?? "nothing"}, available {manifest.Version}.");

        return new UpdateCheck
        {
            Manifest = manifest,
            PlatformKey = key,
            Package = package,
            Installed = installed,
            Reason = reason,
        };
    }

    private UpdateReason DecideReason(InstalledState? installed, string latestVersion, string platformKey)
    {
        if (installed is null)
        {
            return UpdateReason.NotInstalled;
        }

        if (!Directory.Exists(paths.Game))
        {
            return UpdateReason.InstallMissing;
        }

        if (!string.Equals(installed.Platform, platformKey, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateReason.PlatformDiffers;
        }

        return string.Equals(installed.Version, latestVersion, StringComparison.Ordinal)
            ? UpdateReason.None
            : UpdateReason.VersionDiffers;
    }

    /// <summary>Installs the release described by <paramref name="check"/>, unconditionally.</summary>
    public async Task<InstalledState> InstallAsync(
        UpdateCheck check,
        IProgress<UpdateStatus>? progress = null,
        CancellationToken ct = default)
    {
        if (check.LauncherTooOld)
        {
            throw new LauncherTooOldException(
                $"This release needs launcher {check.Manifest.MinLauncherVersion} or newer. " +
                $"This launcher is {LauncherVersion.Current}.");
        }

        if (_launcher.IsGameRunning())
        {
            throw new GameIsRunningException("The game is running. Close it before updating.");
        }

        paths.EnsureCreated();
        _installer.CleanScratch();
        DiskSpace.Require(paths, check.Package.Size);

        var archivePath = Path.Combine(paths.Cache, check.Package.CacheFileName);

        await Download(check, archivePath, progress, ct).ConfigureAwait(false);
        await Verify(check, archivePath, progress, ct).ConfigureAwait(false);
        await ExtractAndPromote(check, archivePath, progress, ct).ConfigureAwait(false);

        var installed = new InstalledState
        {
            Version = check.Manifest.Version,
            Platform = check.PlatformKey,
            InstalledAt = DateTimeOffset.UtcNow,
            Sha256 = check.Package.Sha256,
            Exec = check.Package.Exec,
            LaunchConfirmed = false,
        };

        _state.Write(installed);

        // The archive is only useful until the install succeeds; a gigabyte of cache is not.
        TryDelete(archivePath);

        progress?.Report(UpdateStatus.Of(UpdateStage.Ready, "Pripravené"));
        _log.Info($"Installed {installed.Version} ({installed.Platform}).");

        return installed;
    }

    private async Task Download(
        UpdateCheck check, string archivePath, IProgress<UpdateStatus>? progress, CancellationToken ct)
    {
        var url = new Uri(check.Package.Url);
        _log.Info($"Downloading {DiskSpace.Format(check.Package.Size)} from {url}");

        var downloadProgress = new Progress<DownloadProgress>(p => progress?.Report(
            new UpdateStatus(
                UpdateStage.Downloading,
                "Sťahujem",
                p.Fraction,
                p)));

        await content
            .DownloadToFileAsync(url, archivePath, check.Package.Size, downloadProgress, ct)
            .ConfigureAwait(false);
    }

    private static async Task Verify(
        UpdateCheck check, string archivePath, IProgress<UpdateStatus>? progress, CancellationToken ct)
    {
        var verifyProgress = new Progress<double>(f => progress?.Report(
            new UpdateStatus(UpdateStage.Verifying, "Overujem stiahnuté", f)));

        progress?.Report(UpdateStatus.Of(UpdateStage.Verifying, "Overujem stiahnuté"));

        await Sha256Verifier
            .VerifyOrDeleteAsync(archivePath, check.Package.Sha256, verifyProgress, ct)
            .ConfigureAwait(false);
    }

    private async Task ExtractAndPromote(
        UpdateCheck check, string archivePath, IProgress<UpdateStatus>? progress, CancellationToken ct)
    {
        var extractProgress = new Progress<double>(f => progress?.Report(
            new UpdateStatus(UpdateStage.Extracting, "Rozbaľujem", f)));

        progress?.Report(UpdateStatus.Of(UpdateStage.Extracting, "Rozbaľujem"));

        var extractor = ArchiveExtractors.For(check.Package.ResolvedFormat);
        await extractor.ExtractAsync(archivePath, paths.GameNew, extractProgress, ct).ConfigureAwait(false);

        progress?.Report(UpdateStatus.Of(UpdateStage.Installing, "Inštalujem"));
        _installer.Promote();
    }

    /// <summary>Checks, and installs only if something actually changed.</summary>
    public async Task<UpdateCheck> EnsureLatestAsync(
        IProgress<UpdateStatus>? progress = null,
        CancellationToken ct = default)
    {
        var check = await CheckAsync(progress, ct).ConfigureAwait(false);

        if (!check.UpdateRequired)
        {
            progress?.Report(UpdateStatus.Of(UpdateStage.UpToDate, $"Máš najnovšiu verziu {check.LatestVersion}"));
            return check;
        }

        await InstallAsync(check, progress, ct).ConfigureAwait(false);
        return check with { Installed = _state.Read() };
    }

    /// <summary>
    /// Reinstalls the version the manifest names, whatever is on disk.
    ///
    /// Without this a damaged installation has no way back that a player could be talked through.
    /// Files go missing — antivirus quarantines one, a disk fills up mid-extract, someone deletes
    /// something — and the version check says everything is fine, because it only compares tags.
    /// </summary>
    public async Task<InstalledState> RepairAsync(
        IProgress<UpdateStatus>? progress = null,
        CancellationToken ct = default)
    {
        var check = await CheckAsync(progress, ct).ConfigureAwait(false);

        _log.Info($"Repairing: reinstalling {check.LatestVersion} over whatever is there.");

        // The cached archive is suspect too, since a damaged install may well have come from it.
        var cached = Path.Combine(paths.Cache, check.Package.CacheFileName);
        TryDelete(cached);
        TryDelete(cached + ".part");

        return await InstallAsync(check, progress, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the installed build and, once it has survived a short grace period, records that the
    /// version launched and drops the previous install. A build that dies immediately keeps its
    /// predecessor on disk so <see cref="AtomicInstaller.Rollback"/> has something to restore.
    /// </summary>
    public async Task<Process> LaunchAsync(
        IProgress<UpdateStatus>? progress = null,
        TimeSpan? gracePeriod = null,
        CancellationToken ct = default)
    {
        var installed = _state.Read()
            ?? throw new UpdateException("Nothing is installed yet.");

        progress?.Report(UpdateStatus.Of(UpdateStage.Launching, "Spúšťam hru"));

        var executable = _launcher.ResolveExecutable(paths.Game, installed.Exec);
        var process = _launcher.Start(executable);

        var grace = gracePeriod ?? TimeSpan.FromSeconds(5);
        var survived = !await WaitForExit(process, grace, ct).ConfigureAwait(false);

        if (survived)
        {
            if (!installed.LaunchConfirmed)
            {
                _state.Write(installed with { LaunchConfirmed = true });
            }

            _installer.PruneOldInstall();
        }
        else
        {
            _log.Warn($"The game exited within {grace.TotalSeconds:0}s with code {process.ExitCode}.");
        }

        return process;
    }

    private static async Task<bool> WaitForExit(Process process, TimeSpan within, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(within);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            _log.Warn($"Could not remove the cached archive: {ex.Message}");
        }
    }
}

public class UpdateException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>The game is running, so its files cannot be replaced.</summary>
public sealed class GameIsRunningException(string message) : UpdateException(message);

/// <summary>The manifest declares a launcher version floor this launcher is below.</summary>
public sealed class LauncherTooOldException(string message) : UpdateException(message);
