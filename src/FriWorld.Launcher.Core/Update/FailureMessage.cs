using System.Net.Http;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Localization;
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
///
/// Nothing here ever puts an exception's own message on screen. Those messages are English —
/// they are written for whoever reads the log — and pasting one after a Slovak headline produced
/// exactly the half-translated sentence this launcher used to show. What a player needs out of
/// them is the data, not the wording, so the exceptions that carry data carry it as fields and
/// the sentence is built in the language being spoken.
/// </summary>
public static class FailureMessages
{
    public static FailureMessage Describe(Exception exception, Texts texts) => exception switch
    {
        GameIsRunningException => new(
            texts.GameIsRunningHeadline,
            texts.GameIsRunningAdvice,
            true),

        LauncherTooOldException e => new(
            texts.LauncherTooOldHeadline,
            texts.LauncherTooOldAdvice(e.Required, e.Current),
            false),

        NoBuildForPlatformException => new(
            texts.NoBuildForThisComputer,
            null,
            false),

        InsufficientDiskSpaceException e => new(
            texts.NotEnoughSpaceHeadline,
            texts.NotEnoughSpaceAdvice(e.RequiredBytes, e.AvailableBytes, e.DriveName),
            true),

        HashMismatchException => new(
            texts.CorruptedDownloadHeadline,
            texts.CorruptedDownloadAdvice,
            true),

        ManifestException => new(
            texts.ManifestUnreadableHeadline,
            texts.ManifestUnreadableAdvice,
            false),

        GameLaunchException e => new(
            texts.GameWouldNotStartHeadline,
            texts.GameWouldNotStartAdvice(e.Problem, e.Path),
            true),

        LauncherUpdateException e => new(
            texts.LauncherUpdateFailedHeadline,
            texts.LauncherUpdateFailedAdvice(e.Problem, e.Path),
            false),

        UpdateException => new(
            texts.SomethingWentWrongHeadline,
            texts.TryAgainAdvice,
            true),

        HttpRequestException => new(
            texts.CouldNotReachTheServerHeadline,
            texts.CouldNotReachTheServerAdvice,
            true),

        OperationCanceledException => new(
            texts.Cancelled,
            null,
            true),

        UnauthorizedAccessException => new(
            texts.NoWritePermissionHeadline,
            texts.NoWritePermissionAdvice,
            false),

        IOException => new(
            texts.FileNotWrittenHeadline,
            texts.TryAgainAdvice,
            true),

        _ => new(
            texts.SomethingWentWrongHeadline,
            texts.TryAgainAdvice,
            true),
    };

    /// <summary>
    /// The headline and advice as one line, for the console.
    ///
    /// The console is a developer's, so it gets English and the exception's own message with it:
    /// there the wording is the point, and losing it would mean reaching for the log to learn
    /// what a one-line command already knew.
    /// </summary>
    public static string Flatten(Exception exception)
    {
        var message = Describe(exception, Texts.English);
        var summary = message.Advice is null ? message.Headline : $"{message.Headline} {message.Advice}";

        return string.IsNullOrWhiteSpace(exception.Message) ? summary : $"{summary} ({exception.Message})";
    }
}
