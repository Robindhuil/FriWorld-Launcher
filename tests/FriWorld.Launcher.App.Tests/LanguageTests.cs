using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FriWorld.Launcher.App.ViewModels;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Localization;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// The switch between Slovak and English.
///
/// The thing worth testing is not that the labels have two spellings — that is a lookup table and
/// the core suite checks it. It is that flipping the switch re-says the whole window. Half of what
/// is on screen is a finished sentence written when something happened: a status line, a version
/// line, the release notes. A switch that only relabelled the buttons would leave a window
/// speaking both languages at once, which is worse than not offering the switch at all.
/// </summary>
public class LanguageTests
{
    private static async Task<(MainWindow Window, LauncherViewModel Model)> Ready()
    {
        WindowSandbox.FreshInstallRoot();

        // Installed before the window exists, so it opens on a settled state with sentences in it
        // rather than on a progress bar.
        await LauncherConfiguration
            .Resolve(WindowSandbox.Manifest, WindowSandbox.CurrentInstallRoot)
            .CreateOrchestrator()
            .EnsureLatestAsync();

        var window = new MainWindow();
        window.Show();

        var model = (LauncherViewModel)window.DataContext!;
        await Settle(() => model.Action != LauncherAction.None && !model.Busy, TimeSpan.FromSeconds(20));

        return (window, model);
    }

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

    private static Button ButtonNamed(MainWindow window, string automationName) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == automationName);

    private static void Switch(LauncherViewModel model)
    {
        model.SwitchLanguageCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task The_window_opens_in_slovak()
    {
        var (_, model) = await Ready();

        Assert.Equal(Language.Slovak, model.Texts.Language);
        Assert.Equal("Hrať", model.PrimaryLabel);
    }

    [AvaloniaFact]
    public async Task Switching_relabels_the_buttons()
    {
        var (_, model) = await Ready();

        Switch(model);

        Assert.Equal(Language.English, model.Texts.Language);
        Assert.Equal("Play", model.PrimaryLabel);
        Assert.Equal("Uninstall the game?", Ask(model));
    }

    private static string Ask(LauncherViewModel model)
    {
        model.AskUninstallCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var title = model.QuestionTitle;

        model.CancelQuestionCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        return title;
    }

    [AvaloniaFact]
    public async Task Switching_re_says_the_sentences_already_on_screen()
    {
        // The regression this guards: Status and VersionLine are written once, when the check
        // finishes. Without re-saying them, an English window kept a Slovak status line.
        var (_, model) = await Ready();

        Assert.Equal("Pripravené", model.Status);
        Assert.StartsWith("Verzia ", model.VersionLine);

        Switch(model);

        Assert.Equal("Ready", model.Status);
        Assert.StartsWith("Version ", model.VersionLine);
    }

    [AvaloniaFact]
    public async Task Switching_relabels_what_the_window_itself_binds()
    {
        // Straight through the XAML rather than the view model: the fixed labels bind to Texts,
        // and they only refresh if the change notification reaches them.
        var (window, model) = await Ready();

        Assert.NotNull(ButtonNamed(window, "Zavrieť"));

        Switch(model);

        Assert.NotNull(ButtonNamed(window, "Close"));
    }

    [AvaloniaFact]
    public async Task The_choice_is_remembered_for_next_time()
    {
        var (_, model) = await Ready();
        var paths = new LauncherPaths(WindowSandbox.CurrentInstallRoot);

        Switch(model);

        Assert.Equal(Language.English, LanguagePreference.Read(paths));
    }

    [AvaloniaFact]
    public async Task Switching_re_says_a_failure_too()
    {
        // A failure is the one text a player most needs to understand, and it is written once,
        // when it happens. Pointing the window at a manifest that is not there is the cheapest
        // way to get a real one.
        var manifest = Environment.GetEnvironmentVariable(LauncherConfiguration.ManifestUrlVariable);

        try
        {
            WindowSandbox.FreshInstallRoot();
            Environment.SetEnvironmentVariable(
                LauncherConfiguration.ManifestUrlVariable,
                Path.Combine(Path.GetTempPath(), "friworld-no-such-manifest", "manifest.json"));

            var window = new MainWindow();
            window.Show();

            var model = (LauncherViewModel)window.DataContext!;
            await Settle(() => model.Failed && !model.Busy, TimeSpan.FromSeconds(20));

            Assert.True(model.Failed, "the window did not report a failure to re-say");
            var slovak = model.FailureHeadline;

            Switch(model);

            Assert.NotEqual(slovak, model.FailureHeadline);
            Assert.False(
                model.FailureHeadline.Any("áäčďéíĺľňóôŕšťúýž".Contains),
                $"Slovak left in the English failure: '{model.FailureHeadline}'");
            Assert.Equal(model.FailureHeadline, model.Status);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LauncherConfiguration.ManifestUrlVariable, manifest);
        }
    }

    [AvaloniaFact]
    public async Task Switching_twice_comes_back_to_slovak()
    {
        var (window, model) = await Ready();

        Switch(model);
        Switch(model);

        Assert.Equal(Language.Slovak, model.Texts.Language);
        Assert.Equal("Pripravené", model.Status);
        Assert.NotNull(ButtonNamed(window, "Zavrieť"));
    }
}
