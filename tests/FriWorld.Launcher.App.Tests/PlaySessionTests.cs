using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FriWorld.Launcher.App.ViewModels;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// The whole round trip: the launcher starts the game, gets out of the way, and comes back when
/// the game stops. Reported broken in 0.1.7-alpha — the window returned but sat on
/// "Kontrolujem aktualizácie" and never moved.
/// </summary>
public class PlaySessionTests
{
    private static async Task<(MainWindow Window, LauncherViewModel Model)> Ready()
    {
        WindowSandbox.FreshInstallRoot();

        // Install before the window exists, so the run under test is a plain Play rather than a
        // download followed by one.
        await LauncherConfiguration
            .Resolve(WindowSandbox.Manifest, WindowSandbox.CurrentInstallRoot)
            .CreateOrchestrator()
            .EnsureLatestAsync();

        var window = new MainWindow();
        window.Show();

        var model = (LauncherViewModel)window.DataContext!;

        // Wait for the check to have both started and finished. Waiting only for !Busy can return
        // before it starts, which reads as a stuck launcher when nothing is stuck at all.
        await Settle(() => model.Action != LauncherAction.None && !model.Busy, TimeSpan.FromSeconds(20));

        Assert.True(model.Status == "Pripravené", Describe(model));
        return (window, model);
    }

    /// <summary>Everything worth knowing when one of these fails, since the run is not repeatable.</summary>
    private static string Describe(LauncherViewModel model) =>
        $"{model.Status} [action {model.Action}, busy {model.Busy}, failed {model.Failed}, " +
        $"headline '{model.FailureHeadline}', progress {model.ProgressVisible}]";

    /// <summary>Pumps the dispatcher until <paramref name="done"/> holds or the time runs out.</summary>
    private static async Task Settle(Func<bool> done, TimeSpan within)
    {
        var deadline = DateTime.UtcNow + within;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (done())
            {
                return;
            }

            await Task.Delay(50);
        }

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task The_launcher_comes_back_ready_after_the_game_stops()
    {
        var (window, model) = await Ready();

        model.PrimaryCommand.Execute(null);

        // The stub runs for two seconds; the window is hidden for that long and no longer.
        await Settle(() => !window.IsVisible, TimeSpan.FromSeconds(15));
        Assert.False(window.IsVisible, "the launcher never got out of the way");

        await Settle(
            () => window.IsVisible && !model.Busy && model.Action != LauncherAction.None,
            TimeSpan.FromSeconds(30));

        Assert.True(window.IsVisible, "the launcher never came back");
        Assert.False(model.Busy, "the launcher came back still working");

        // The bug: it came back showing the text the check sets before it starts, and stayed.
        Assert.True(model.Status == "Pripravené", Describe(model));
        Assert.False(model.ProgressVisible, "the progress bar was left on screen");
    }
}
