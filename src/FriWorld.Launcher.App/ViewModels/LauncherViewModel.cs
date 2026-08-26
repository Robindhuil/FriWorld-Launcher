using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.App.ViewModels;

/// <summary>
/// Drives the single launcher window.
///
/// It owns no update logic of its own — everything is <see cref="UpdateOrchestrator"/>, the same
/// code the headless front end runs. This class only turns progress reports into strings and
/// decides which button is enabled.
/// </summary>
public sealed class LauncherViewModel : ObservableObject
{
    private readonly UpdateOrchestrator _orchestrator;
    private readonly SingleInstanceLock? _instanceLock;
    private CancellationTokenSource? _work;

    private string _status = "Starting";
    private string _detail = string.Empty;
    private string _versionLine = string.Empty;
    private string _notes = string.Empty;
    private double _progress;
    private bool _progressIndeterminate = true;
    private bool _progressVisible;
    private bool _busy;
    private bool _canPlay;
    private bool _failed;

    public LauncherViewModel()
    {
        var configuration = LauncherConfiguration.Resolve();
        _instanceLock = SingleInstanceLock.TryAcquire(configuration.Paths);
        _orchestrator = configuration.CreateOrchestrator();

        PlayCommand = new RelayCommand(() => _ = PlayAsync(), () => CanPlay && !Busy);
        RetryCommand = new RelayCommand(() => _ = RefreshAsync(), () => !Busy);
    }

    public RelayCommand PlayCommand { get; }

    public RelayCommand RetryCommand { get; }

    public string Title => "FriWorld";

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetField(ref _detail, value);
    }

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
        private set => SetField(ref _progressVisible, value);
    }

    public bool Failed
    {
        get => _failed;
        private set => SetField(ref _failed, value);
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (SetField(ref _busy, value))
            {
                PlayCommand.RaiseCanExecuteChanged();
                RetryCommand.RaiseCanExecuteChanged();
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
                PlayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Checks for an update and installs it. Called once when the window opens.</summary>
    public async Task RefreshAsync()
    {
        if (_instanceLock is null)
        {
            Fail("Another launcher is already running.");
            return;
        }

        _work?.Cancel();
        _work = new CancellationTokenSource();

        Busy = true;
        Failed = false;
        CanPlay = false;
        ProgressVisible = true;

        try
        {
            var check = await _orchestrator.EnsureLatestAsync(new UiProgress(this), _work.Token);

            VersionLine = $"Version {check.LatestVersion}";
            Notes = check.Manifest.Notes ?? string.Empty;
            Status = "Ready to play";
            Detail = string.Empty;
            ProgressVisible = false;
            CanPlay = true;
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled";
            ProgressVisible = false;
        }
        catch (Exception ex)
        {
            // An install already on disk stays playable even when the check could not complete,
            // which matters when the player is simply offline.
            var installed = _orchestrator.State.Read();
            CanPlay = installed is not null;

            Fail(ex.Message);

            if (installed is not null)
            {
                VersionLine = $"Version {installed.Version} (offline)";
            }
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task PlayAsync()
    {
        Busy = true;

        try
        {
            Status = "Starting the game";
            await _orchestrator.LaunchAsync(new UiProgress(this));
            Status = "Running";
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
        finally
        {
            Busy = false;
        }
    }

    private void Fail(string message)
    {
        Failed = true;
        Status = "Something went wrong";
        Detail = message;
        ProgressVisible = false;
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

    /// <summary>Marshals progress reports onto the UI thread.</summary>
    private sealed class UiProgress(LauncherViewModel owner) : IProgress<UpdateStatus>
    {
        public void Report(UpdateStatus value) =>
            Dispatcher.UIThread.Post(() => owner.Apply(value));
    }
}
