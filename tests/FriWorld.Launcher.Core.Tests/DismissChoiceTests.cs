using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Escape has five possible outcomes and picking the wrong one costs a download or an install,
/// so the ordering between them is worth pinning down.
/// </summary>
public class DismissChoiceTests
{
    [Fact]
    public void With_nothing_going_on_it_asks_about_closing()
    {
        Assert.Equal(
            DismissOutcome.AskToClose,
            DismissChoice.ForEscape(
                confirmingUninstall: false, confirmingClose: false, canCancel: false, busy: false));
    }

    [Fact]
    public void A_running_download_is_cancelled_rather_than_the_window_closed()
    {
        Assert.Equal(
            DismissOutcome.CancelTheWork,
            DismissChoice.ForEscape(
                confirmingUninstall: false, confirmingClose: false, canCancel: true, busy: true));
    }

    [Fact]
    public void Work_that_cannot_be_stopped_is_left_alone()
    {
        // Unpacking and the directory swap. Escape is a reflex and must not be able to kill the
        // process in the middle of one.
        Assert.Equal(
            DismissOutcome.Ignore,
            DismissChoice.ForEscape(
                confirmingUninstall: false, confirmingClose: false, canCancel: false, busy: true));
    }

    [Fact]
    public void The_uninstall_question_is_answered_the_safe_way_first()
    {
        Assert.Equal(
            DismissOutcome.KeepTheGame,
            DismissChoice.ForEscape(
                confirmingUninstall: true, confirmingClose: false, canCancel: false, busy: false));
    }

    [Fact]
    public void The_closing_question_backs_out_rather_than_closing()
    {
        // Escape raised the question; the same key must not then answer it yes.
        Assert.Equal(
            DismissOutcome.StayOpen,
            DismissChoice.ForEscape(
                confirmingUninstall: false, confirmingClose: true, canCancel: false, busy: false));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void The_uninstall_question_outranks_everything_else(bool canCancel, bool busy)
    {
        Assert.Equal(
            DismissOutcome.KeepTheGame,
            DismissChoice.ForEscape(confirmingUninstall: true, confirmingClose: true, canCancel, busy));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Escape_never_closes_on_its_own(bool confirmingUninstall, bool canCancel)
    {
        // The property that matters more than any single case. Every rung of the ladder either
        // answers a question, stops work, or asks — none of them closes the window outright.
        var outcome = DismissChoice.ForEscape(
            confirmingUninstall, confirmingClose: false, canCancel, busy: false);

        Assert.True(
            outcome is DismissOutcome.KeepTheGame
                or DismissOutcome.CancelTheWork
                or DismissOutcome.AskToClose,
            $"Escape reached {outcome}");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Busy_never_reaches_the_closing_question(bool confirmingUninstall, bool canCancel)
    {
        // Being asked whether to close while an install is halfway through is an invitation to
        // answer yes. While work is running Escape either stops it or does nothing.
        Assert.NotEqual(
            DismissOutcome.AskToClose,
            DismissChoice.ForEscape(confirmingUninstall, confirmingClose: false, canCancel, busy: true));
    }
}
