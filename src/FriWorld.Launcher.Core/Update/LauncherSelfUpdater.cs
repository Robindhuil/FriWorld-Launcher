using System.Diagnostics;
using FriWorld.Launcher.Core.Diagnostics;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Update;

/// <summary>
/// Replaces the launcher with a newer one.
///
/// This is the most dangerous code in the project: it overwrites the very program that would
/// otherwise be able to repair things. Every step is therefore written so that failing at that
/// step leaves a working launcher behind.
///
/// The Windows trick it rests on is that a running executable cannot be overwritten but can be
/// renamed. So the running file is renamed aside, the new one is written to the original path,
/// and the old one is deleted by the next start — not by this one, which is still running from it.
/// </summary>
public sealed class LauncherSelfUpdater(
    IContentClient content,
    ILauncherLog? log = null,
    LauncherDeployment? deployment = null)
{
    /// <summary>Suffix for the outgoing executable, cleaned up on the next start.</summary>
    public const string SupersededSuffix = ".superseded";

    private readonly ILauncherLog _log = log ?? NullLauncherLog.Instance;
    private readonly LauncherDeployment _deployment = deployment ?? LauncherDeployment.Current;

    /// <summary>The file this process is running from, or null when that cannot be determined.</summary>
    public string? ExecutablePath => _deployment.ExecutablePath;

    /// <summary>
    /// Whether the launcher is deployed as something it can swap in one move.
    ///
    /// Only a single-file build qualifies. A build spread over dozens of DLLs cannot be replaced
    /// atomically, and a half-replaced launcher is worse than an old one, so those are told to
    /// download manually instead.
    /// </summary>
    public bool IsSelfContainedSingleFile =>
        _deployment.IsSingleFile && ExecutablePath is { } path && File.Exists(path);

    /// <summary>Why a self-update is not on offer, or null when it is.</summary>
    public string? BlockedReason()
    {
        if (ExecutablePath is null)
        {
            return "The launcher cannot tell which file it is running from.";
        }

        if (!IsSelfContainedSingleFile)
        {
            return "This launcher is not a single-file build, so it cannot replace itself safely.";
        }

        return CanWriteBeside(ExecutablePath)
            ? null
            : $"The launcher cannot write to {Path.GetDirectoryName(ExecutablePath)}.";
    }

    /// <summary>
    /// Removes the executable left behind by a previous update. Call once at startup, before
    /// anything else; it is the second half of the rename trick.
    /// </summary>
    public void CleanUpSupersededExecutable()
    {
        if (ExecutablePath is not { } path)
        {
            return;
        }

        var superseded = path + SupersededSuffix;

        if (!File.Exists(superseded))
        {
            return;
        }

        try
        {
            File.Delete(superseded);
            _log.Info("Removed the previous launcher executable.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Antivirus sometimes holds the file briefly. It will be retried next start, and a
            // stray file is not worth failing over.
            _log.Warn($"Could not remove {superseded} yet: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches the new launcher and verifies it, without touching the running one. Returns the
    /// path of the staged file.
    /// </summary>
    public async Task<string> StageAsync(
        LauncherBinary binary,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!binary.IsUsable)
        {
            throw new LauncherUpdateException(
                "The manifest's launcher binary is not usable. It needs an https url, a 64 character sha256 and a size.");
        }

        var path = ExecutablePath
            ?? throw new LauncherUpdateException("The launcher cannot tell which file it is running from.");

        var staged = path + ".incoming";

        _log.Info($"Downloading launcher {binary.Size} bytes from {binary.Url}");

        await content
            .DownloadToFileAsync(new Uri(binary.Url), staged, binary.Size, progress, ct)
            .ConfigureAwait(false);

        // Verified before anything is renamed. A launcher that swapped in an unverified file
        // would be a remote code execution bug, not a bug in updating.
        await Sha256Verifier.VerifyOrDeleteAsync(staged, binary.Sha256, null, ct).ConfigureAwait(false);

        _log.Info("The new launcher passed its checksum.");
        return staged;
    }

    /// <summary>
    /// Puts the staged executable in place and starts it. On success this process should exit
    /// immediately; on failure the running launcher is left exactly as it was.
    /// </summary>
    public void Apply(string stagedPath, bool restart = true)
    {
        if (BlockedReason() is { } reason)
        {
            throw new LauncherUpdateException(reason);
        }

        var path = ExecutablePath!;
        var superseded = path + SupersededSuffix;

        if (!File.Exists(stagedPath))
        {
            throw new LauncherUpdateException($"The staged launcher is missing: {stagedPath}");
        }

        // A leftover from an earlier attempt would block the rename below.
        if (File.Exists(superseded))
        {
            File.Delete(superseded);
        }

        File.Move(path, superseded);

        try
        {
            File.Move(stagedPath, path);
        }
        catch (Exception ex)
        {
            // Nothing has been lost yet: put the running executable back where it was and give up.
            _log.Error("Installing the new launcher failed; restoring the current one.", ex);

            try
            {
                File.Move(superseded, path);
            }
            catch (Exception restoreFailure)
            {
                // Both moves failed, which is the one genuinely bad outcome. Say exactly where
                // the executable went, because that is now the only way back.
                _log.Error($"Could not restore the launcher. It is at {superseded}.", restoreFailure);

                throw new LauncherUpdateException(
                    $"The update failed and the launcher could not be put back. " +
                    $"Rename '{superseded}' to '{Path.GetFileName(path)}' to recover.");
            }

            throw new LauncherUpdateException($"Installing the new launcher failed: {ex.Message}");
        }

        _log.Info("The launcher has been replaced.");

        if (!restart)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = false,
        });
    }

    /// <summary>Discards a staged download, for a cancelled or abandoned update.</summary>
    public static void DiscardStaged(string stagedPath)
    {
        try
        {
            if (File.Exists(stagedPath))
            {
                File.Delete(stagedPath);
            }
        }
        catch (IOException)
        {
            // A leftover in the launcher's own folder is untidy, not harmful.
        }
    }

    private static bool CanWriteBeside(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath);

        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed class LauncherUpdateException(string message) : Exception(message);
