namespace FriWorld.Launcher.Core.Update;

/// <summary>What pressing Escape should do, given what the window is currently doing.</summary>
public enum DismissOutcome
{
    /// <summary>Answer the uninstall question with the safe answer.</summary>
    KeepTheGame,

    /// <summary>Answer the closing question with the safe answer.</summary>
    StayOpen,

    /// <summary>Stop the download that is running.</summary>
    CancelTheWork,

    /// <summary>Nothing at all.</summary>
    Ignore,

    /// <summary>Ask whether to close.</summary>
    AskToClose,
}

/// <summary>
/// The Escape ladder. Lives here rather than in the window so the ordering can be tested; it has
/// five outcomes and the wrong one loses a download or an install.
/// </summary>
public static class DismissChoice
{
    /// <summary>
    /// Backs out of whatever is innermost.
    ///
    /// Questions come first, because a question is the thing most recently put in front of the
    /// person, and Escape always answers it the safe way — a key press must never be the one that
    /// deletes a game or shuts the window.
    ///
    /// A running download comes next: stopping it is what Escape is for, and a partial download is
    /// kept anyway.
    ///
    /// Work that cannot be stopped is where Escape does <em>nothing</em>. Escape is a reflex, and a
    /// reflex must not be able to kill the process midway through unpacking or swapping
    /// directories.
    /// </summary>
    public static DismissOutcome ForEscape(
        bool confirmingUninstall,
        bool confirmingClose,
        bool canCancel,
        bool busy)
    {
        if (confirmingUninstall)
        {
            return DismissOutcome.KeepTheGame;
        }

        if (confirmingClose)
        {
            return DismissOutcome.StayOpen;
        }

        if (canCancel)
        {
            return DismissOutcome.CancelTheWork;
        }

        return busy ? DismissOutcome.Ignore : DismissOutcome.AskToClose;
    }
}
