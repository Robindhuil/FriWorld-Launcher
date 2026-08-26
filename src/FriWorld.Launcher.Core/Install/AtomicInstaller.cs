using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Install;

/// <summary>
/// Puts a freshly extracted build into place.
///
/// The swap is two directory renames rather than a copy, because a rename on the same volume is
/// near-instant and cannot half-succeed the way copying a gigabyte of files can. Nothing ever
/// writes into the live <c>game</c> directory: it is only ever renamed away or renamed into.
/// </summary>
public sealed class AtomicInstaller(LauncherPaths paths, ILauncherLog? log = null)
{
    private readonly ILauncherLog _log = log ?? NullLauncherLog.Instance;

    /// <summary>
    /// Promotes <c>game.new</c> to <c>game</c>, moving any existing install to <c>game.old</c>.
    /// The old install is deliberately left on disk; see <see cref="PruneOldInstall"/>.
    /// </summary>
    public void Promote()
    {
        if (!Directory.Exists(paths.GameNew))
        {
            throw new InvalidOperationException($"Nothing to promote: {paths.GameNew} does not exist.");
        }

        // A leftover game.old from an interrupted run would block the rename.
        DeleteIfPresent(paths.GameOld);

        var hadPrevious = Directory.Exists(paths.Game);

        if (hadPrevious)
        {
            Directory.Move(paths.Game, paths.GameOld);
        }

        try
        {
            Directory.Move(paths.GameNew, paths.Game);
        }
        catch (Exception ex)
        {
            _log.Error("Promoting the new install failed; restoring the previous one.", ex);

            if (hadPrevious && !Directory.Exists(paths.Game))
            {
                Directory.Move(paths.GameOld, paths.Game);
            }

            throw;
        }

        _log.Info(hadPrevious
            ? "Swapped in the new install; the previous one is kept until it has started once."
            : "Installed for the first time.");
    }

    /// <summary>
    /// Removes the previous install. Call only once the new build has started successfully,
    /// otherwise there is nothing to fall back to.
    /// </summary>
    public void PruneOldInstall()
    {
        if (!Directory.Exists(paths.GameOld))
        {
            return;
        }

        try
        {
            Directory.Delete(paths.GameOld, recursive: true);
            _log.Info("Removed the previous install.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Antivirus or a lingering handle can hold files briefly. It will be retried next run.
            _log.Warn($"Could not remove the previous install yet: {ex.Message}");
        }
    }

    /// <summary>Puts the previous install back, for when the new build cannot start at all.</summary>
    public bool Rollback()
    {
        if (!Directory.Exists(paths.GameOld))
        {
            return false;
        }

        DeleteIfPresent(paths.Game);
        Directory.Move(paths.GameOld, paths.Game);
        _log.Warn("Rolled back to the previous install.");
        return true;
    }

    /// <summary>Clears anything a previous failed attempt left behind.</summary>
    public void CleanScratch()
    {
        DeleteIfPresent(paths.GameNew);
    }

    private static void DeleteIfPresent(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
