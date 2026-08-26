using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.App.ViewModels;

/// <summary>
/// Drives the single launcher window.
///
/// It owns no update logic of its own — everything is <see cref="UpdateOrchestrator"/>, the same
/// code the headless front end runs. This class decides which buttons are live and turns progress
/// and failures into sentences.
///
/// The one rule shaping all of it: an installed game stays playable. A new release, an unreachable
/// server and a failed download are all things that must not stand between a player and a game
/// that is already on their disk.
/// </summary>
public sealed class LauncherViewModel : ObservableObject
{
    private readonly UpdateOrchestrator _orchestrator;
    private readonly LauncherSelfUpdater _selfUpdater;
    private readonly ILauncherLog _log;
    private readonly Lock _workGate = new();

    private readonly bool _keepOpenAfterLaunch;

    private SingleInstanceLock? _instanceLock;
    private CancellationTokenSource? _work;
    private UpdateCheck? _check;
    private LauncherBinary? _launcherBinary;
    private string? _launcherDownloadPage;

    private string _status = "Starting";
    private string _detail = string.Empty;
    private string _versionLine = string.Empty;
    private string _notes = string.Empty;
    private string _failureAdvice = string.Empty;
    private string _launcherUpdateNotice = string.Empty;
    private double _progress;
    private bool _progressIndeterminate = true;
    private bool _progressVisible;
    private bool _busy;
    private bool _canPlay;
    private bool _canUpdate;
    private bool _canCancel;
    private bool _failed;

    public LauncherViewModel()
    {
        var configuration = LauncherConfiguration.Resolve();
        _keepOpenAfterLaunch = LauncherSettingsFile.Load().KeepOpenAfterLaunch;
        _log = configuration.Log;
        _instanceLock = SingleInstanceLock.TryAcquire(configuration.Paths);
        _orchestrator = configuration.CreateOrchestrator();
        _selfUpdater = new LauncherSelfUpdater(CompositeContentClient.CreateDefault(), _log);

        // The second half of the rename trick from the last self-update, if there was one.
        _selfUpdater.CleanUpSupersededExecutable();

        PlayCommand = new RelayCommand(() => _ = PlayAsync(), () => CanPlay && !Busy);
        UpdateCommand = new RelayCommand(() => _ = InstallAsync(), () => CanUpdate && !Busy);
        RepairCommand = new RelayCommand(() => _ = RepairAsync(), () => !Busy);
        RetryCommand = new RelayCommand(() => _ = RefreshAsync(), () => !Busy);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        UpdateLauncherCommand = new RelayCommand(() => _ = UpdateLauncherAsync(), () => !Busy);
    }

    public RelayCommand PlayCommand { get; }

    public RelayCommand UpdateCommand { get; }

    public RelayCommand RepairCommand { get; }

    public RelayCommand RetryCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand UpdateLauncherCommand { get; }

    /// <summary>
    /// Raised once the game is up and the launcher has nothing left to do. The window listens
    /// and closes itself; the view model does not reach into the UI to do it.
    /// </summary>
    public event EventHandler? CloseRequested;

    public string Title => "FriWorld";

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string Detail
    {
        get => _detail;
        private set
        {
            if (SetField(ref _detail, value))
            {
                Raise(nameof(InfoVisible));
            }
        }
    }

    /// <summary>
    /// Whether the neutral detail line should show.
    ///
    /// Detail doubles as reassurance ("you can keep playing the version you have") and as the
    /// body of a failure. Without this the reassuring cases were written and never rendered,
    /// because the only two places showing Detail were the progress panel and the error panel.
    /// </summary>
    public bool InfoVisible => !Failed && !ProgressVisible && !string.IsNullOrEmpty(Detail);

    /// <summary>Whether there is an installation worth repairing.</summary>
    public bool IsInstalled => _orchestrator.State.Read() is not null;

    public string VersionLine
    {
        get => _versionLine;
        private set => SetField(ref _versionLine, value);
    }

    public string Notes
    {
        get => _notes;
        private set => SetField(ref _notes, value);
    }

    public string FailureAdvice
    {
        get => _failureAdvice;
        private set => SetField(ref _failureAdvice, value);
    }

    public string LauncherUpdateNotice
    {
        get => _launcherUpdateNotice;
        private set => SetField(ref _launcherUpdateNotice, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        private set => SetField(ref _progressIndeterminate, value);
    }

    public bool ProgressVisible
    {
        get => _progressVisible;
        private set
        {
            if (SetField(ref _progressVisible, value))
            {
                Raise(nameof(InfoVisible));
            }
        }
    }

    public bool Failed
    {
        get => _failed;
        private set
        {
            if (SetField(ref _failed, value))
            {
                Raise(nameof(InfoVisible));
            }
        }
    }

    public bool LauncherUpdateAvailable => _launcherDownloadPage is not null;

    /// <summary>Whether the newer launcher can be installed here, rather than only linked to.</summary>
    public bool LauncherUpdateIsAutomatic => _launcherBinary is not null && _selfUpdater.BlockedReason() is null;

    public string LauncherUpdateAction => LauncherUpdateIsAutomatic ? "Update and restart" : "Open download page";

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetField(ref _busy, value))
            {
                RaiseAll();
            }
        }
    }

    public bool CanPlay
    {
        get => _canPlay;
        private set
        {
            if (SetField(ref _canPlay, value))
            {
                RaiseAll();
            }
        }
    }

    /// <summary>An update is available and waiting for the player to say yes.</summary>
    public bool CanUpdate
    {
        get => _canUpdate;
        private set
        {
            if (SetField(ref _canUpdate, value))
            {
                RaiseAll();
            }
        }
    }

    public bool CanCancel
    {
        get => _canCancel;
        private set
        {
            if (SetField(ref _canCancel, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Looks for an update, and installs one only when there is nothing playable yet.
    ///
    /// A player who already has the game gets asked. Downloading hundreds of megabytes because
    /// someone opened the launcher is not a decision the launcher gets to make for them.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_instanceLock is null)
        {
            Fail(new UpdateException("Another launcher is already running."));
            return;
        }

        Reset();
        Busy = true;

        try
        {
            _check = await Run(ct => _orchestrator.CheckAsync(new UiProgress(this), ct));

            ApplyLauncherUpdate(_check);
            Notes = _check.Manifest.Notes ?? string.Empty;

            if (_check.LauncherTooOld)
            {
                ShowLauncherTooOld(_check);
                return;
            }

            if (!_check.UpdateRequired)
            {
                Ready(_check.LatestVersion, "Ready to play");
                return;
            }

            if (_check.CanPlayWithoutUpdating)
            {
                OfferChoice(_check);
                return;
            }

            // Nothing playable is installed, so there is no choice to offer.
            await InstallAsync();
        }
        catch (OperationCanceledException)
        {
            Cancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Downloads and installs the release the last check found.</summary>
    private async Task InstallAsync()
    {
        if (_check is not { } check)
        {
            return;
        }

        Busy = true;
        Failed = false;
        CanUpdate = false;
        CanPlay = false;
        CanCancel = true;

        try
        {
            await Run(ct => _orchestrator.InstallAsync(check, new UiProgress(this), ct));
            _check = check with { Installed = _orchestrator.State.Read() };
            Ready(check.LatestVersion, "Ready to play");
        }
        catch (OperationCanceledException)
        {
            Cancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            CanCancel = false;
            Busy = false;
        }
    }

    private async Task RepairAsync()
    {
        Busy = true;
        Failed = false;
        CanPlay = false;
        CanUpdate = false;
        CanCancel = true;

        try
        {
            Status = "Repairing the installation";
            await Run(ct => _orchestrator.RepairAsync(new UiProgress(this), ct));
            _check = _check is null ? null : _check with { Installed = _orchestrator.State.Read() };
            Ready(_orchestrator.State.Read()?.Version ?? string.Empty, "Repaired and ready");
        }
        catch (OperationCanceledException)
        {
            Cancelled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            CanCancel = false;
            Busy = false;
        }
    }

    private async Task PlayAsync()
    {
        Busy = true;

        try
        {
            Status = "Starting the game";

            // LaunchAsync already waits out the grace period that confirms the build can start,
            // so by the time it returns the answer is known and the previous install is gone.
            var process = await _orchestrator.LaunchAsync(new UiProgress(this));

            if (process.HasExited)
            {
                // The game stopped within seconds of starting. Staying open is the whole point
                // here: this is the one moment when the launcher has something useful to say.
                Failed = true;
                Status = "The game closed straight away";
                Detail = $"It exited with code {process.ExitCode} moments after starting.";
                FailureAdvice = "Repairing the installation may help. The log has the details.";
                ProgressVisible = false;
                _log.Warn($"The game exited with code {process.ExitCode} during the grace period.");
                return;
            }

            Status = "Running";

            if (!_keepOpenAfterLaunch)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Replaces the launcher when the manifest carries a binary for this machine, otherwise opens
    /// the download page. Both paths end with a person having a newer launcher.
    /// </summary>
    private async Task UpdateLauncherAsync()
    {
        if (!LauncherUpdateIsAutomatic)
        {
            if (_launcherDownloadPage is { } page && !SystemBrowser.TryOpen(page))
            {
                Detail = $"Could not open the browser. The download page is {page}";
            }

            return;
        }

        Busy = true;
        Failed = false;
        CanCancel = true;
        string? staged = null;

        try
        {
            Status = "Downloading the new launcher";
            ProgressVisible = true;

            staged = await Run(ct => _selfUpdater.StageAsync(_launcherBinary!, new UiDownloadProgress(this), ct));

            Status = "Restarting";

            // Released before the successor starts. It takes the same lock as its first act, and
            // this process is still alive at that moment — holding on would make the new launcher
            // report that another one is running and look like the update had broken things.
            _instanceLock?.Dispose();
            _instanceLock = null;

            _selfUpdater.Apply(staged);

            // Apply started the replacement; this process must get out of its way.
            Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            if (staged is not null)
            {
                LauncherSelfUpdater.DiscardStaged(staged);
            }

            Cancelled();
        }
        catch (Exception ex)
        {
            if (staged is not null)
            {
                LauncherSelfUpdater.DiscardStaged(staged);
            }

            Fail(ex);
        }
        finally
        {
            CanCancel = false;
            Busy = false;
        }
    }

    private void Cancel()
    {
        Status = "Cancelling";

        // Guarded because the work can finish between the button appearing and the click landing,
        // and cancelling a disposed source throws — from a command handler, that ends the process.
        lock (_workGate)
        {
            try
            {
                _work?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already finished. Nothing to cancel.
            }
        }
    }

    private async Task<T> Run<T>(Func<CancellationToken, Task<T>> work)
    {
        CancellationTokenSource source;

        lock (_workGate)
        {
            _work?.Dispose();
            _work = source = new CancellationTokenSource();
        }

        try
        {
            return await work(source.Token);
        }
        finally
        {
            lock (_workGate)
            {
                if (ReferenceEquals(_work, source))
                {
                    _work.Dispose();
                    _work = null;
                }
            }
        }
    }

    private void Reset()
    {
        Failed = false;
        FailureAdvice = string.Empty;
        CanPlay = false;
        CanUpdate = false;
        CanCancel = false;
        ProgressVisible = true;
        ProgressIndeterminate = true;
        Status = "Checking for updates";
        Detail = string.Empty;
    }

    private void Ready(string version, string status)
    {
        VersionLine = string.IsNullOrEmpty(version) ? string.Empty : $"Version {version}";
        Status = status;
        Detail = string.Empty;
        ProgressVisible = false;
        CanPlay = true;
        CanUpdate = false;
    }

    /// <summary>Both options live at once: play what is installed, or take the new release.</summary>
    private void OfferChoice(UpdateCheck check)
    {
        VersionLine = $"Version {check.InstalledVersion} installed · {check.LatestVersion} available";
        Status = $"Update to {check.LatestVersion}?";
        Detail = $"You can keep playing {check.InstalledVersion} for now.";
        ProgressVisible = false;
        CanPlay = true;
        CanUpdate = true;
    }

    private void ShowLauncherTooOld(UpdateCheck check)
    {
        Failed = true;
        Status = "This launcher is too old";
        Detail = $"Release {check.LatestVersion} needs launcher {check.Manifest.MinLauncherVersion} or newer.";
        FailureAdvice = LauncherUpdateAvailable
            ? "Use the launcher update below."
            : "Download a newer launcher from the FriWorld Hub.";
        ProgressVisible = false;

        // Whatever is installed still runs; only updating the game is off the table.
        CanPlay = check.Installed is not null;
        CanUpdate = false;
    }

    private void Cancelled()
    {
        Status = "Cancelled";
        Detail = "A partial download is kept and will continue next time.";
        ProgressVisible = false;
        CanPlay = _orchestrator.State.Read() is not null;
        CanUpdate = _check?.UpdateRequired ?? false;
    }

    /// <summary>
    /// Shows the failure and, just as importantly, writes it down.
    ///
    /// The window holds one sentence and it is gone when the player closes it. A launcher that
    /// fails on someone else's machine is diagnosed from <c>launcher.log</c> or not at all.
    /// </summary>
    private void Fail(Exception exception)
    {
        _log.Error("Launcher operation failed.", exception);

        var message = FailureMessages.Describe(exception);

        Failed = true;
        Status = message.Headline;
        Detail = message.Advice ?? string.Empty;
        FailureAdvice = string.Empty;
        ProgressVisible = false;

        // An installation already on disk stays playable, which is what matters when the failure
        // was simply that the server could not be reached.
        var installed = _orchestrator.State.Read();
        CanPlay = installed is not null;
        CanUpdate = message.Recoverable && (_check?.UpdateRequired ?? false);

        if (installed is not null && string.IsNullOrEmpty(VersionLine))
        {
            VersionLine = $"Version {installed.Version} installed";
        }
    }

    private void ApplyLauncherUpdate(UpdateCheck check)
    {
        if (check.LauncherUpdate is not { } launcher)
        {
            _launcherDownloadPage = null;
            _launcherBinary = null;
            LauncherUpdateNotice = string.Empty;
        }
        else
        {
            _launcherDownloadPage = launcher.DownloadUrl;
            _launcherBinary = check.LauncherBinary;

            LauncherUpdateNotice = string.IsNullOrWhiteSpace(launcher.Notes)
                ? $"Launcher {launcher.Version} is available."
                : $"Launcher {launcher.Version} is available. {launcher.Notes}";
        }

        Raise(nameof(LauncherUpdateAvailable));
        Raise(nameof(LauncherUpdateIsAutomatic));
        Raise(nameof(LauncherUpdateAction));
    }

    private void Apply(UpdateStatus status)
    {
        Status = status.Message;
        ProgressIndeterminate = status.Fraction is null;
        Progress = (status.Fraction ?? 0) * 100;
        ProgressVisible = status.Stage is not (UpdateStage.UpToDate or UpdateStage.Ready);

        Detail = status.Download is { } download
            ? $"{DiskSpace.Format(download.BytesReceived)} of " +
              $"{(download.TotalBytes is { } total ? DiskSpace.Format(total) : "?")}" +
              (download.Remaining is { } left ? $" · {left:mm\\:ss} left" : string.Empty)
            : string.Empty;
    }

    private void ApplyDownload(DownloadProgress download)
    {
        ProgressIndeterminate = download.Fraction is null;
        Progress = (download.Fraction ?? 0) * 100;
        ProgressVisible = true;
        Detail = $"{DiskSpace.Format(download.BytesReceived)} of " +
                 $"{(download.TotalBytes is { } total ? DiskSpace.Format(total) : "?")}";
    }

    private void RaiseAll()
    {
        PlayCommand.RaiseCanExecuteChanged();
        UpdateCommand.RaiseCanExecuteChanged();
        RepairCommand.RaiseCanExecuteChanged();
        RetryCommand.RaiseCanExecuteChanged();
        UpdateLauncherCommand.RaiseCanExecuteChanged();
        Raise(nameof(IsInstalled));
    }

    /// <summary>Marshals progress reports onto the UI thread.</summary>
    private sealed class UiProgress(LauncherViewModel owner) : IProgress<UpdateStatus>
    {
        public void Report(UpdateStatus value) => Dispatcher.UIThread.Post(() => owner.Apply(value));
    }

    private sealed class UiDownloadProgress(LauncherViewModel owner) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => Dispatcher.UIThread.Post(() => owner.ApplyDownload(value));
    }
}
