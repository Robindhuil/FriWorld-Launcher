using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.App.ViewModels;

/// <summary>
/// Drives the single launcher window.
///
/// It owns no update logic of its own — everything is <see cref="UpdateOrchestrator"/>, the same
/// code the headless front end runs. This class decides what the buttons say and turns progress
/// and failures into sentences.
///
/// Two rules shape all of it.
///
/// Nothing large happens on its own. Opening the launcher checks what is available and then waits.
/// Downloading hundreds of megabytes because someone opened a window is not the launcher's call.
///
/// An installed game stays playable. A new release, an unreachable server and a failed download
/// are all things that must not stand between a player and a game already on their disk.
/// </summary>
public sealed class LauncherViewModel : ObservableObject
{
    private static readonly IBrush AccentDot = SolidColorBrush.Parse("#FBB800");
    private static readonly IBrush ErrorDot = SolidColorBrush.Parse("#FF7A6E");
    private static readonly IBrush NeutralDot = SolidColorBrush.Parse("#73FFFFFF");

    private readonly UpdateOrchestrator _orchestrator;
    private readonly LauncherSelfUpdater _selfUpdater;
    private readonly ILauncherLog _log;
    private readonly LauncherPaths _paths;
    private readonly Lock _workGate = new();
    private readonly bool _keepOpenAfterLaunch;

    private SingleInstanceLock? _instanceLock;
    private CancellationTokenSource? _work;
    private UpdateCheck? _check;
    private LauncherBinary? _launcherBinary;
    private string? _launcherDownloadPage;

    private LauncherAction _action = LauncherAction.None;
    private string _status = "Spúšťam";
    private string _phaseName = string.Empty;
    private string _percentText = string.Empty;
    private string _detail = string.Empty;
    private string _versionLine = string.Empty;
    private string _notes = string.Empty;
    private string _failureHeadline = string.Empty;
    private string _failureAdvice = string.Empty;
    private string _launcherUpdateTitle = string.Empty;
    private string _launcherUpdateNote = string.Empty;
    private double _progress;
    private bool _progressIndeterminate = true;
    private bool _progressVisible;
    private bool _busy;
    private bool _canCancel;
    private bool _failed;
    private bool _confirmingUninstall;
    private bool _confirmingClose;

    public LauncherViewModel()
    {
        var configuration = LauncherConfiguration.Resolve();
        _keepOpenAfterLaunch = LauncherSettingsFile.Load().KeepOpenAfterLaunch;
        _log = configuration.Log;
        _paths = configuration.Paths;
        _instanceLock = SingleInstanceLock.TryAcquire(configuration.Paths);
        _orchestrator = configuration.CreateOrchestrator();
        _selfUpdater = new LauncherSelfUpdater(CompositeContentClient.CreateDefault(), _log);

        // The second half of the rename trick from the last self-update, if there was one.
        _selfUpdater.CleanUpSupersededExecutable();

        PrimaryCommand = new RelayCommand(RunPrimary, () => PrimaryEnabled);
        SecondaryCommand = new RelayCommand(RunSecondary, () => SecondaryVisible && !Busy);
        CancelCommand = new RelayCommand(Cancel, () => CanCancel);
        UpdateLauncherCommand = new RelayCommand(() => _ = UpdateLauncherAsync(), () => !Busy);
        MinimiseCommand = new RelayCommand(() => MinimiseRequested?.Invoke(this, EventArgs.Empty));
        CloseCommand = new RelayCommand(() => ConfirmingClose = true);
        ConfirmCloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        CancelCloseCommand = new RelayCommand(() => ConfirmingClose = false);
        RevealCommand = new RelayCommand(Reveal, () => IsInstalled);
        AskUninstallCommand = new RelayCommand(() => ConfirmingUninstall = true, () => IsInstalled && !Busy);
        RepairCommand = new RelayCommand(() => _ = RepairAsync(), () => IsInstalled && !Busy);
        RecheckCommand = new RelayCommand(() => _ = RefreshAsync(), () => !Busy);
        OpenLogCommand = new RelayCommand(OpenLog);
        DismissCommand = new RelayCommand(Dismiss);
        ConfirmUninstallCommand = new RelayCommand(Uninstall, () => !Busy);
        CancelUninstallCommand = new RelayCommand(() => ConfirmingUninstall = false);
    }

    /// <summary>The one prominent button. What it does depends on <see cref="Action"/>.</summary>
    public RelayCommand PrimaryCommand { get; }

    /// <summary>The quieter button beside it: play the old build, or repair the current one.</summary>
    public RelayCommand SecondaryCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand UpdateLauncherCommand { get; }

    public RelayCommand MinimiseCommand { get; }

    /// <summary>Asks whether to close. Closing is never one key press or one stray click away.</summary>
    public RelayCommand CloseCommand { get; }

    public RelayCommand ConfirmCloseCommand { get; }

    public RelayCommand CancelCloseCommand { get; }

    /// <summary>Shows the installed game in the file manager.</summary>
    public RelayCommand RevealCommand { get; }

    /// <summary>Reinstalls the version already on disk, for when its files have gone bad.</summary>
    public RelayCommand RepairCommand { get; }

    /// <summary>Asks the manifest again, for when the first answer arrived before the network did.</summary>
    public RelayCommand RecheckCommand { get; }

    /// <summary>Opens the launcher's own log. The one thing worth having when someone reports a problem.</summary>
    public RelayCommand OpenLogCommand { get; }

    /// <summary>Escape. Backs out of whatever is innermost, and asks to close only when nothing is.</summary>
    public RelayCommand DismissCommand { get; }

    /// <summary>Asks about uninstalling. Deleting is never one click away.</summary>
    public RelayCommand AskUninstallCommand { get; }

    public RelayCommand ConfirmUninstallCommand { get; }

    public RelayCommand CancelUninstallCommand { get; }

    public event EventHandler? MinimiseRequested;

    /// <summary>Raised when the person asks to close, and only then.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised while the game is running, and again when it stops.</summary>
    public event EventHandler<bool>? VisibilityRequested;

    /// <summary>
    /// Whether to offer minimising. A download of several hundred megabytes takes long enough
    /// that someone will reasonably want to do something else meanwhile.
    /// </summary>
    public bool ShowMinimise => true;

    public LauncherAction Action
    {
        get => _action;
        private set
        {
            if (SetField(ref _action, value))
            {
                RaiseButtons();
            }
        }
    }

    public string PrimaryLabel => Action switch
    {
        LauncherAction.Install => "Inštalovať",
        LauncherAction.Update => "Aktualizovať",
        LauncherAction.Play => "Hrať",
        LauncherAction.Retry => "Skúsiť znova",
        _ => "Počkaj chvíľu",
    };

    public bool PrimaryEnabled => Action != LauncherAction.None && !Busy;

    /// <summary>
    /// The secondary button exists for one thing only: keeping the build already installed when
    /// an update is offered. Repairing moved to its own icon in the action bar, because it is
    /// available whenever a game is installed and not only when there is nothing else to do.
    /// </summary>
    public string SecondaryLabel => Action switch
    {
        LauncherAction.Update => $"Hrať {_check?.InstalledVersion}",
        _ => string.Empty,
    };

    public bool SecondaryVisible => Action is LauncherAction.Update;

    /// <summary>
    /// Whether anything is installed. Gates the two actions that only mean something with a game
    /// on disk: showing it in the file manager, and removing it.
    /// </summary>
    public bool IsInstalled => _orchestrator.State.Read() is not null;

    /// <summary>
    /// Whether the uninstall question is showing.
    ///
    /// Uninstalling deletes hundreds of megabytes and cannot be undone, so it gets a question
    /// rather than a click. The question sits in the window rather than in a dialog, because
    /// everything else this launcher has to say sits there too.
    /// </summary>
    public bool ConfirmingUninstall
    {
        get => _confirmingUninstall;
        private set
        {
            if (SetField(ref _confirmingUninstall, value))
            {
                Raise(nameof(NotesVisible));
                Raise(nameof(InfoVisible));
                Raise(nameof(PlainTextVisible));
                Raise(nameof(FailureVisible));
            }
        }
    }

    /// <summary>
    /// Whether the closing question is showing.
    ///
    /// Closing mid-download is not destructive — a partial file is kept — but the launcher is one
    /// window with one job, and a stray click on an X in the corner should not end it. The window
    /// closing itself after the game starts does not go through here; that is not someone asking.
    /// </summary>
    public bool ConfirmingClose
    {
        get => _confirmingClose;
        private set
        {
            if (SetField(ref _confirmingClose, value))
            {
                Raise(nameof(NotesVisible));
                Raise(nameof(InfoVisible));
                Raise(nameof(PlainTextVisible));
                Raise(nameof(FailureVisible));
                Raise(nameof(CloseQuestionDetail));
            }
        }
    }

    /// <summary>
    /// The second line of the closing question. It says what closing costs, and the honest answer
    /// depends on what is running — pretending a download is lost when it resumes would be as bad
    /// as staying silent when it is.
    /// </summary>
    public string CloseQuestionDetail => CanCancel
        ? "Sťahovanie sa zastaví. Stiahnuté súbory zostanú a nabudúce sa bude pokračovať tam, kde prestalo."
        : Busy
            ? "Launcher práve pracuje. Zavretie teraz nechá rozrobenú prácu, ktorú bude treba spraviť znova."
            : "Hra zostane nainštalovaná.";

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    /// <summary>Colour of the dot beside the status line: amber when ready, red on failure.</summary>
    public IBrush StatusDot => Failed
        ? ErrorDot
        : Action == LauncherAction.Play ? AccentDot : NeutralDot;

    public string PhaseName
    {
        get => _phaseName;
        private set => SetField(ref _phaseName, value);
    }

    public string PercentText
    {
        get => _percentText;
        private set
        {
            if (SetField(ref _percentText, value))
            {
                Raise(nameof(HasPercent));
            }
        }
    }

    public bool HasPercent => !string.IsNullOrEmpty(PercentText);

    public string Detail
    {
        get => _detail;
        private set
        {
            if (SetField(ref _detail, value))
            {
                Raise(nameof(InfoVisible));
                Raise(nameof(PlainTextVisible));
            }
        }
    }

    /// <summary>The extra line under the release notes, such as the download size.</summary>
    public bool InfoVisible => NotesVisible && !string.IsNullOrEmpty(Detail);

    /// <summary>A bare sentence, for states with neither notes nor progress to show.</summary>
    public bool PlainTextVisible =>
        !Failed && !ProgressVisible && !AskingSomething && !NotesVisible && !string.IsNullOrEmpty(Detail);

    /// <summary>Whether a question owns the middle of the window. Two of them can never overlap.</summary>
    public bool AskingSomething => ConfirmingUninstall || ConfirmingClose;

    public string VersionLine
    {
        get => _versionLine;
        private set => SetField(ref _versionLine, value);
    }

    public string Notes
    {
        get => _notes;
        private set
        {
            if (SetField(ref _notes, value))
            {
                Raise(nameof(NotesVisible));
                Raise(nameof(InfoVisible));
                Raise(nameof(PlainTextVisible));
            }
        }
    }

    public bool NotesVisible => !Failed && !ProgressVisible && !AskingSomething && !string.IsNullOrEmpty(Notes);

    public string FailureHeadline
    {
        get => _failureHeadline;
        private set => SetField(ref _failureHeadline, value);
    }

    public string FailureAdvice
    {
        get => _failureAdvice;
        private set
        {
            if (SetField(ref _failureAdvice, value))
            {
                Raise(nameof(HasFailureAdvice));
            }
        }
    }

    public bool HasFailureAdvice => !string.IsNullOrEmpty(FailureAdvice);

    public string LauncherUpdateTitle
    {
        get => _launcherUpdateTitle;
        private set => SetField(ref _launcherUpdateTitle, value);
    }

    public string LauncherUpdateNote
    {
        get => _launcherUpdateNote;
        private set
        {
            if (SetField(ref _launcherUpdateNote, value))
            {
                Raise(nameof(HasLauncherUpdateNote));
            }
        }
    }

    public bool HasLauncherUpdateNote => !string.IsNullOrEmpty(LauncherUpdateNote);

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
                Raise(nameof(PlainTextVisible));
                Raise(nameof(NotesVisible));
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
                Raise(nameof(PlainTextVisible));
                Raise(nameof(NotesVisible));
                Raise(nameof(StatusDot));
                Raise(nameof(FailureVisible));
            }
        }
    }

    /// <summary>
    /// Whether the failure block is on screen. The uninstall question takes the same space and
    /// must win it, otherwise the two draw on top of each other.
    /// </summary>
    public bool FailureVisible => Failed && !AskingSomething;

    public bool LauncherUpdateAvailable => _launcherDownloadPage is not null;

    /// <summary>Whether the newer launcher can be installed here, rather than only linked to.</summary>
    public bool LauncherUpdateIsAutomatic => _launcherBinary is not null && _selfUpdater.BlockedReason() is null;

    public string LauncherUpdateAction =>
        LauncherUpdateIsAutomatic ? "Aktualizovať a reštartovať" : "Otvoriť stránku so stiahnutím";

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetField(ref _busy, value))
            {
                RaiseButtons();
                Raise(nameof(CloseQuestionDetail));
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
                Raise(nameof(CloseQuestionDetail));
            }
        }
    }

    /// <summary>
    /// Looks at what is available and decides what the button should offer. Installs nothing.
    /// Called once when the window opens.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_instanceLock is null)
        {
            Fail(new UpdateException("Už beží iný launcher."));
            Action = LauncherAction.None;
            return;
        }

        Reset();
        Busy = true;

        try
        {
            _check = await Run(ct => _orchestrator.CheckAsync(new UiProgress(this), ct));

            ApplyLauncherUpdate(_check);
            Notes = _check.Manifest.Notes ?? string.Empty;
            ProgressVisible = false;

            if (_check.LauncherTooOld)
            {
                ShowLauncherTooOld(_check);
                return;
            }

            Action = LauncherActions.AfterCheck(_check);
            Describe(_check);
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

    /// <summary>Puts the state into words once the action has been decided.</summary>
    private void Describe(UpdateCheck check)
    {
        switch (Action)
        {
            case LauncherAction.Play:
                VersionLine = $"Verzia {check.LatestVersion}";
                Status = "Pripravené";
                Detail = string.Empty;
                break;

            case LauncherAction.Update:
                VersionLine = $"Verzia {check.InstalledVersion} nainštalovaná · " +
                              $"{check.LatestVersion} k dispozícii";
                Status = $"Aktualizovať na {check.LatestVersion}?";
                Detail = $"Zatiaľ môžeš hrať {check.InstalledVersion}.";
                break;

            case LauncherAction.Install:
                VersionLine = $"Verzia {check.LatestVersion} k dispozícii";
                Status = "Nenainštalované";
                Detail = $"Na stiahnutie {Size(check.Package.Size)}.";
                break;
        }
    }

    /// <summary>Slovak writes a decimal comma; a dot reads as a thousands separator.</summary>
    private static string Size(long bytes) => DiskSpace.Format(bytes).Replace('.', ',');

    private void RunPrimary()
    {
        switch (Action)
        {
            case LauncherAction.Install:
            case LauncherAction.Update:
                _ = InstallAsync();
                break;

            case LauncherAction.Play:
                _ = PlayAsync();
                break;

            case LauncherAction.Retry:
                _ = RefreshAsync();
                break;
        }
    }

    private void RunSecondary()
    {
        switch (Action)
        {
            case LauncherAction.Update:
                _ = PlayAsync();
                break;

            case LauncherAction.Play:
                _ = RepairAsync();
                break;
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
        Action = LauncherAction.None;
        CanCancel = true;

        try
        {
            await Run(ct => _orchestrator.InstallAsync(check, new UiProgress(this), ct));
            _check = check with { Installed = _orchestrator.State.Read() };

            VersionLine = $"Verzia {check.LatestVersion}";
            Status = "Pripravené";
            Detail = string.Empty;
            ProgressVisible = false;
            Action = LauncherAction.Play;
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
        Action = LauncherAction.None;
        CanCancel = true;

        try
        {
            Status = "Opravujem inštaláciu";
            var installed = await Run(ct => _orchestrator.RepairAsync(new UiProgress(this), ct));
            _check = _check is null ? null : _check with { Installed = installed };

            VersionLine = $"Verzia {installed.Version}";
            Status = "Opravené a pripravené";
            Detail = string.Empty;
            ProgressVisible = false;
            Action = LauncherAction.Play;
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
        Action = LauncherAction.None;

        var hidden = false;

        try
        {
            Status = "Spúšťam hru";

            // LaunchAsync already waits out the grace period that confirms the build can start,
            // so by the time it returns the answer is known and the previous install is gone.
            var process = await _orchestrator.LaunchAsync(new UiProgress(this));

            if (process.HasExited)
            {
                // The game stopped within seconds of starting. Staying open is the whole point
                // here: this is the one moment when the launcher has something useful to say.
                ShowFailure(
                    "Hra sa hneď zavrela.",
                    $"Skončila s kódom {process.ExitCode} pár sekúnd po spustení. " +
                    "Môže pomôcť oprava inštalácie.");

                Action = LauncherAction.Play;
                _log.Warn($"The game exited with code {process.ExitCode} during the grace period.");
                return;
            }

            Status = "Beží";
            Action = LauncherAction.Play;

            // Out of the way while the game has the screen, back again when it does not. Closing
            // instead would mean the person has to find the launcher a second time to do the one
            // thing they are most likely to want next, which is to stop or update.
            if (!_keepOpenAfterLaunch)
            {
                hidden = true;
                VisibilityRequested?.Invoke(this, false);
            }

            await process.WaitForExitAsync().ConfigureAwait(true);

            _log.Info($"The game exited with code {process.ExitCode}.");
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            // In the finally and not after the wait: anything thrown while the window is hidden
            // would otherwise leave a launcher with no window and no way to reach it.
            if (hidden)
            {
                VisibilityRequested?.Invoke(this, true);
            }

            Busy = false;
        }

        // A session can run for an hour, so the launcher that comes back looks at the world again
        // rather than showing what was true before the game started.
        if (!Failed)
        {
            await RefreshAsync();
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
                Detail = $"Prehliadač sa nepodarilo otvoriť. Stránka je {page}";
            }

            return;
        }

        Busy = true;
        Failed = false;
        CanCancel = true;
        string? staged = null;

        try
        {
            PhaseName = "Sťahujem nový launcher";
            ProgressVisible = true;

            staged = await Run(ct => _selfUpdater.StageAsync(_launcherBinary!, new UiDownloadProgress(this), ct));

            Status = "Reštartujem";

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
        Status = "Ruším";

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
        FailureHeadline = string.Empty;
        FailureAdvice = string.Empty;
        Action = LauncherAction.None;
        CanCancel = false;
        ProgressVisible = true;
        ProgressIndeterminate = true;
        PhaseName = "Kontrolujem aktualizácie";
        PercentText = string.Empty;
        Status = "Kontrolujem aktualizácie";
        Detail = "Zisťujem, čo je na serveri.";
    }

    private void ShowLauncherTooOld(UpdateCheck check)
    {
        ShowFailure(
            "Tento launcher je príliš starý.",
            $"Vydanie {check.LatestVersion} potrebuje launcher " +
            $"{check.Manifest.MinLauncherVersion} alebo novší.");

        Status = "Launcher je príliš starý";

        // Whatever is installed still runs; only updating the game is off the table.
        Action = LauncherActions.AfterCheck(check);
    }

    private void Cancelled()
    {
        Failed = false;
        Notes = string.Empty;
        ProgressVisible = false;
        Status = "Zrušené";
        Detail = "Čiastočne stiahnuté súbory sme nechali, nabudúce sa bude pokračovať tam, kde si prestal.";
        Action = LauncherActions.AfterInterruption(_check, _orchestrator.State.Read() is not null);
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
        ShowFailure(message.Headline, message.Advice ?? string.Empty);

        Status = message.Headline;

        Action = message.Recoverable
            ? LauncherActions.AfterInterruption(_check, _orchestrator.State.Read() is not null)
            : LauncherAction.None;

        if (_orchestrator.State.Read() is { } installed && string.IsNullOrEmpty(VersionLine))
        {
            VersionLine = $"Verzia {installed.Version} nainštalovaná";
        }
    }

    private void ShowFailure(string headline, string advice)
    {
        Failed = true;
        FailureHeadline = headline;
        FailureAdvice = advice;
        ProgressVisible = false;
    }

    private void ApplyLauncherUpdate(UpdateCheck check)
    {
        if (check.LauncherUpdate is not { } launcher)
        {
            _launcherDownloadPage = null;
            _launcherBinary = null;
            LauncherUpdateTitle = string.Empty;
            LauncherUpdateNote = string.Empty;
        }
        else
        {
            _launcherDownloadPage = launcher.DownloadUrl;
            _launcherBinary = check.LauncherBinary;
            LauncherUpdateTitle = $"K dispozícii je launcher {launcher.Version}.";
            LauncherUpdateNote = launcher.Notes ?? string.Empty;
        }

        Raise(nameof(LauncherUpdateAvailable));
        Raise(nameof(LauncherUpdateIsAutomatic));
        Raise(nameof(LauncherUpdateAction));
    }

    private void Apply(UpdateStatus status)
    {
        // Nothing is running, so this report is from something that already finished. Applying it
        // would put a phase name and a progress bar back over the answer.
        if (!Busy)
        {
            return;
        }

        PhaseName = status.Message;
        Status = StatusLineFor(status);
        PercentText = status.PercentText;
        ProgressIndeterminate = status.Fraction is null;
        Progress = (status.Fraction ?? 0) * 100;
        ProgressVisible = status.Stage is not (UpdateStage.UpToDate or UpdateStage.Ready);
        Detail = status.DetailLine;
    }

    /// <summary>
    /// The action bar names the whole job; the progress row names the current phase. Both change,
    /// but the bar changes less, which is what stops the window feeling like four screens.
    /// </summary>
    private string StatusLineFor(UpdateStatus status)
    {
        var version = _check?.LatestVersion;

        return status.Stage switch
        {
            UpdateStage.Downloading => $"Sťahujem {version}",
            UpdateStage.Verifying => "Overujem stiahnuté",
            UpdateStage.Extracting or UpdateStage.Installing => $"Inštalujem {version}",
            UpdateStage.Launching => "Spúšťam hru",
            _ => status.Message,
        };
    }

    private void ApplyDownload(DownloadProgress download)
    {
        if (!Busy)
        {
            return;
        }

        ProgressIndeterminate = download.Fraction is null;
        Progress = (download.Fraction ?? 0) * 100;
        ProgressVisible = true;
        PercentText = download.Fraction is { } f ? $"{f * 100:0} %" : string.Empty;
        Detail = new UpdateStatus(UpdateStage.Downloading, PhaseName, download.Fraction, download).DetailLine;
    }

    private void RaiseButtons()
    {
        Raise(nameof(PrimaryLabel));
        Raise(nameof(PrimaryEnabled));
        Raise(nameof(SecondaryLabel));
        Raise(nameof(SecondaryVisible));
        Raise(nameof(StatusDot));
        Raise(nameof(IsInstalled));

        PrimaryCommand.RaiseCanExecuteChanged();
        SecondaryCommand.RaiseCanExecuteChanged();
        UpdateLauncherCommand.RaiseCanExecuteChanged();
        RevealCommand.RaiseCanExecuteChanged();
        AskUninstallCommand.RaiseCanExecuteChanged();
        ConfirmUninstallCommand.RaiseCanExecuteChanged();
        RepairCommand.RaiseCanExecuteChanged();
        RecheckCommand.RaiseCanExecuteChanged();
    }

    private void Reveal()
    {
        var path = _orchestrator.InstalledExecutablePath();

        if (path is null || !SystemFileManager.TryReveal(path))
        {
            Detail = "Priečinok s hrou sa nepodarilo otvoriť.";
        }
    }

    /// <summary>
    /// What Escape does, innermost first: answer the uninstall question with "keep", stop a
    /// download, or close the window.
    ///
    /// It deliberately does nothing while the launcher is working on something it cannot stop.
    /// Escape is a reflex, and a reflex must not be able to kill a process midway through
    /// unpacking or swapping directories. Closing then stays possible, but only by choosing to
    /// click the button.
    /// </summary>
    private void Dismiss()
    {
        switch (DismissChoice.ForEscape(ConfirmingUninstall, ConfirmingClose, CanCancel, Busy))
        {
            case DismissOutcome.KeepTheGame:
                ConfirmingUninstall = false;
                break;

            case DismissOutcome.StayOpen:
                ConfirmingClose = false;
                break;

            case DismissOutcome.CancelTheWork:
                Cancel();
                break;

            case DismissOutcome.AskToClose:
                ConfirmingClose = true;
                break;
        }
    }

    private void OpenLog()
    {
        if (!SystemFileManager.TryReveal(_paths.LogFile))
        {
            Detail = "Denník launchera sa nepodarilo otvoriť.";
        }
    }

    private void Uninstall()
    {
        ConfirmingUninstall = false;
        Busy = true;

        try
        {
            _orchestrator.Uninstall();
            _check = _check is null ? null : _check with { Installed = null };

            VersionLine = _check is null ? string.Empty : $"Verzia {_check.LatestVersion} k dispozícii";
            Notes = _check?.Manifest.Notes ?? string.Empty;
            Status = "Odinštalované";
            Detail = _check is null
                ? "Hra bola odstránená."
                : $"Hra bola odstránená. Na stiahnutie {Size(_check.Package.Size)}.";
            Failed = false;
            ProgressVisible = false;

            // Saves live outside the install, so this genuinely is only the game coming off.
            Action = _check is null ? LauncherAction.Retry : LauncherAction.Install;
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

    /// <summary>Marshals progress reports onto the UI thread.</summary>
    /// <summary>
    /// Marshals progress reports onto the UI thread — and applies them straight away when it is
    /// already on it.
    ///
    /// Posting unconditionally is what broke the launcher after a game session. A report raised on
    /// the UI thread went into the queue, and when the work that raised it finished without ever
    /// suspending, the result was written first and the queued report then put the phase name and
    /// the progress bar back over it. The window sat on "Kontrolujem aktualizácie" for good.
    /// </summary>
    private sealed class UiProgress(LauncherViewModel owner) : IProgress<UpdateStatus>
    {
        public void Report(UpdateStatus value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                owner.Apply(value);
                return;
            }

            Dispatcher.UIThread.Post(() => owner.Apply(value));
        }
    }

    private sealed class UiDownloadProgress(LauncherViewModel owner) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                owner.ApplyDownload(value);
                return;
            }

            Dispatcher.UIThread.Post(() => owner.ApplyDownload(value));
        }
    }
}
