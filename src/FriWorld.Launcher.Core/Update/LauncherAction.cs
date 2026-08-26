namespace FriWorld.Launcher.Core.Update;

/// <summary>
/// What the launcher can do next.
///
/// This lives here rather than with the window because it is not a question about buttons. It is
/// the answer to "given what is on disk and what the manifest says, what is the player's move" —
/// which the headless front end has to know too, and which is worth testing without a UI attached.
/// </summary>
public enum LauncherAction
{
    /// <summary>Busy, or stuck with nothing useful to offer.</summary>
    None,

    /// <summary>Nothing on disk yet.</summary>
    Install,

    /// <summary>Something newer is available than what is installed.</summary>
    Update,

    /// <summary>Installed and current.</summary>
    Play,

    /// <summary>Something failed and trying again is worth a shot.</summary>
    Retry,
}

public static class LauncherActions
{
    /// <summary>
    /// What to offer after a successful check.
    ///
    /// Note what this never returns on its own: nothing here starts a download. A launcher that
    /// installs because a window opened has taken a decision that costs someone hundreds of
    /// megabytes, and it is not the launcher's to take.
    /// </summary>
    public static LauncherAction AfterCheck(UpdateCheck check)
    {
        if (check.LauncherTooOld)
        {
            // The game cannot be updated, but whatever is already installed still runs.
            return check.Installed is not null ? LauncherAction.Play : LauncherAction.None;
        }

        if (!check.UpdateRequired)
        {
            return LauncherAction.Play;
        }

        return check.CanPlayWithoutUpdating ? LauncherAction.Update : LauncherAction.Install;
    }

    /// <summary>
    /// What to offer after something was cancelled or failed recoverably.
    ///
    /// An installed game wins: being able to play is more use to someone than being able to
    /// retry a download that just failed.
    /// </summary>
    public static LauncherAction AfterInterruption(UpdateCheck? check, bool anythingInstalled)
    {
        if (!anythingInstalled)
        {
            return check is null ? LauncherAction.Retry : LauncherAction.Install;
        }

        return check?.UpdateRequired == true ? LauncherAction.Update : LauncherAction.Play;
    }
}
