using System.Net.Http;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Update;

/// <param name="Headline">One short line naming what went wrong.</param>
/// <param name="Advice">What the person can do about it, or null when there is nothing useful to say.</param>
/// <param name="Recoverable">Whether trying again could plausibly work.</param>
public readonly record struct FailureMessage(string Headline, string? Advice, bool Recoverable);

/// <summary>
/// Turns exceptions into something worth showing a person.
///
/// The raw message is fine for the log and useless in a window: "The remote name could not be
/// resolved" tells a player nothing they can act on. This is the one place that translation
/// happens, so the window and the console front end cannot describe the same failure differently.
/// </summary>
public static class FailureMessages
{
    public static FailureMessage Describe(Exception exception) => exception switch
    {
        GameIsRunningException => new(
            "The game is already running.",
            "Close it and try again.",
            true),

        LauncherTooOldException e => new(
            "This launcher is too old for the current release.",
            e.Message + " Download a newer launcher.",
            false),

        InsufficientDiskSpaceException e => new(
            "Not enough free space.",
            e.Message + " An update needs room for the download and the new files at the same time.",
            true),

        HashMismatchException => new(
            "The download was damaged.",
            "The file did not match its checksum and was deleted. Trying again usually fixes it.",
            true),

        ManifestException => new(
            "The release information could not be read.",
            "The server answered, but not with something this launcher understands.",
            false),

        GameLaunchException e => new(
            "The game could not be started.",
            e.Message + " Repairing the installation may help.",
            true),

        LauncherUpdateException e => new(
            "The launcher could not update itself.",
            e.Message,
            false),

        UpdateException e => new(
            e.Message,
            null,
            true),

        HttpRequestException => new(
            "Could not reach the download server.",
            "Check the connection and try again.",
            true),

        TaskCanceledException or OperationCanceledException => new(
            "Cancelled.",
            null,
            true),

        UnauthorizedAccessException => new(
            "The launcher is not allowed to write where it installs.",
            "Check the folder's permissions, or run the launcher from a different location.",
            false),

        IOException e => new(
            "A file could not be written.",
            e.Message,
            true),

        _ => new(
            "Something went wrong.",
            exception.Message,
            true),
    };

    /// <summary>The headline and advice as one line, for the console.</summary>
    public static string Flatten(Exception exception)
    {
        var message = Describe(exception);
        return message.Advice is null ? message.Headline : $"{message.Headline} {message.Advice}";
    }
}
