using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FriWorld.Launcher.App.ViewModels;

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// Drives the real window headlessly. Whether a key reaches the focused control or the default
/// button is a property of Avalonia's routing, not of anything readable in the view model, so it
/// gets asserted against the actual window rather than reasoned about.
/// </summary>
public class KeyboardTests
{
    private static (MainWindow Window, LauncherViewModel Model) Open()
    {
        WindowSandbox.FreshInstallRoot();

        var window = new MainWindow();
        window.Show();

        // The window kicks off a check when it loads. It reads a local mock manifest, so this is
        // a few file reads rather than a network call, but it still has to settle before the
        // buttons mean anything.
        Dispatcher.UIThread.RunJobs();

        return (window, (LauncherViewModel)window.DataContext!);
    }

    private static Button ButtonNamed(MainWindow window, string automationName) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == automationName);

    [AvaloniaFact]
    public void Enter_presses_the_button_that_has_the_focus()
    {
        // The bug this exists for: a window-wide Enter binding ran the main button no matter what
        // Tab had landed on, so moving the focus did nothing and the ring lied about what Enter
        // would do.
        var (window, model) = Open();

        var close = ButtonNamed(window, "Zavrieť");
        close.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        Assert.True(close.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(model.ConfirmingClose, "Enter did not reach the focused button.");
    }

    [AvaloniaFact]
    public void Tab_moves_the_focus_between_buttons()
    {
        var (window, _) = Open();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var first = Focused(window);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var second = Focused(window);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [AvaloniaFact]
    public void The_main_button_is_the_default_one()
    {
        // Enter with nothing focused still has to do the obvious thing. IsDefault is Avalonia's
        // way of saying so, and it only fires for an Enter nothing else took.
        var (window, _) = Open();

        var primary = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => b.Classes.Contains("primary"));

        Assert.True(primary.IsDefault);
    }

    [AvaloniaFact]
    public void Escape_asks_before_closing_and_backs_out_of_the_question()
    {
        var (window, model) = Open();

        Assert.False(model.ConfirmingClose);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.ConfirmingClose, "Escape did not raise the closing question.");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.ConfirmingClose, "Escape did not back out of the closing question.");
    }

    [AvaloniaFact]
    public void The_window_does_not_close_itself_when_the_question_is_asked()
    {
        // The question is worth nothing if the window has already gone.
        var (window, model) = Open();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(model.ConfirmingClose);
        Assert.True(window.IsVisible);
    }

    private static object? Focused(MainWindow window) => window.FocusManager?.GetFocusedElement();
}
