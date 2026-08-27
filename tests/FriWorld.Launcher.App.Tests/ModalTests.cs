using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FriWorld.Launcher.App.ViewModels;

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// A question is a modal, and "modal" is three separate promises: it is on top, nothing behind it
/// can be clicked, and nothing behind it can be reached with the keyboard either. The last one is
/// the one a scrim alone does not keep.
/// </summary>
public class ModalTests
{
    private static (MainWindow Window, LauncherViewModel Model) Open()
    {
        WindowSandbox.FreshInstallRoot();

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, (LauncherViewModel)window.DataContext!);
    }

    private static Button ButtonNamed(MainWindow window, string automationName) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == automationName);

    private static Control Content(MainWindow window) =>
        window.GetVisualDescendants().OfType<Grid>().First(g => g.RowDefinitions.Count == 3);

    private static void AskToClose(MainWindow window)
    {
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Everything_behind_the_question_goes_inert()
    {
        var (window, model) = Open();
        var content = Content(window);

        Assert.True(content.IsEffectivelyEnabled);

        AskToClose(window);

        Assert.True(model.AskingSomething);
        Assert.False(content.IsEffectivelyEnabled, "the window behind the question stayed live");
    }

    [AvaloniaFact]
    public void The_close_button_behind_it_cannot_be_reached()
    {
        // Disabled controls are skipped by tab navigation, which is the half of modal that a
        // scrim on its own does not give you.
        var (window, _) = Open();

        AskToClose(window);

        Assert.False(ButtonNamed(window, "Zavrieť").IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void Enter_does_not_reach_the_main_button()
    {
        // The main button is the default one. Without this the modal would be a picture and Enter
        // would start an install straight through it.
        var (window, model) = Open();

        AskToClose(window);

        Assert.False(model.PrimaryEnabled, "the main button was still armed behind the question");
    }

    [AvaloniaFact]
    public void The_safe_answer_has_the_focus_when_it_opens()
    {
        var (window, _) = Open();

        AskToClose(window);
        Dispatcher.UIThread.RunJobs();

        var focused = window.FocusManager?.GetFocusedElement() as Button;

        Assert.NotNull(focused);
        Assert.Equal("Späť", focused!.Content);
    }

    [AvaloniaFact]
    public void Answering_puts_the_window_back()
    {
        var (window, model) = Open();
        var content = Content(window);

        AskToClose(window);
        model.CancelQuestionCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(model.AskingSomething);
        Assert.True(content.IsEffectivelyEnabled, "the window stayed inert after the question went");
    }

    [AvaloniaFact]
    public void One_question_at_a_time()
    {
        // Both flags feed one modal, so a second question opening over the first would show one
        // title with the other one's buttons.
        var (window, model) = Open();

        AskToClose(window);

        Assert.True(model.ConfirmingClose);
        Assert.False(model.ConfirmingUninstall);
        Assert.Equal("Zavrieť launcher?", model.QuestionTitle);
        Assert.Equal("Späť", model.SafeAnswerLabel);
    }
}
