using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Escape has four possible outcomes and picking the wrong one costs a download or an install,
/// so the ordering between them is worth pinning down.
/// </summary>
public class DismissChoiceTests
{
    [Fact]
    public void With_nothing_going_on_it_closes_the_window()
    {
        Assert.Equal(
            DismissOutcome.CloseTheWindow,
            DismissChoice.ForEscape(confirmingUninstall: false, canCancel: false, busy: false));
    }

    [Fact]
    public void A_running_download_is_cancelled_rather_than_the_window_closed()
    {
        Assert.Equal(
            DismissOutcome.CancelTheWork,
            DismissChoice.ForEscape(confirmingUninstall: false, canCancel: true, busy: true));
    }

    [Fact]
    public void Work_that_cannot_be_stopped_is_left_alone()
    {
        // Unpacking and the directory swap. Escape is a reflex and must not be able to kill the
        // process in the middle of one; the close button still can, because that is a decision.
        Assert.Equal(
            DismissOutcome.Ignore,
            DismissChoice.ForEscape(confirmingUninstall: false, canCancel: false, busy: true));
    }

    [Fact]
    public void The_uninstall_question_is_answered_the_safe_way_first()
    {
        // Innermost wins, and the answer a key press gives is always the one that keeps the game.
        Assert.Equal(
            DismissOutcome.KeepTheGame,
            DismissChoice.ForEscape(confirmingUninstall: true, canCancel: false, busy: false));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void The_question_outranks_everything_else(bool canCancel, bool busy)
    {
        Assert.Equal(
            DismissOutcome.KeepTheGame,
            DismissChoice.ForEscape(confirmingUninstall: true, canCancel, busy));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Busy_never_closes_the_window(bool confirmingUninstall, bool canCancel)
    {
        // The property that matters more than any single case. Closing mid-install is the one
        // outcome with a cost, so no combination of states may reach it while work is running.
        Assert.NotEqual(
            DismissOutcome.CloseTheWindow,
            DismissChoice.ForEscape(confirmingUninstall, canCancel, busy: true));
    }
}
