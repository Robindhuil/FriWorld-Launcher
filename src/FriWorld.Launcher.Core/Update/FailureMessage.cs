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
///
/// The text is Slovak because it is read by players — schoolchildren, in this game's case.
/// Developer-facing output such as the CLI's help stays English; the split is by audience,
/// not by project.
/// </summary>
public static class FailureMessages
{
    public static FailureMessage Describe(Exception exception) => exception switch
    {
        GameIsRunningException => new(
            "Hra už beží.",
            "Zavri ju a skús to znova.",
            true),

        LauncherTooOldException e => new(
            "Tento launcher je príliš starý.",
            e.Message + " Stiahni si novší.",
            false),

        InsufficientDiskSpaceException e => new(
            "Nedostatok voľného miesta.",
            e.Message + " Treba miesto na stiahnutie aj rozbalenie naraz.",
            true),

        HashMismatchException => new(
            "Stiahnutý súbor je poškodený.",
            "Nesedel kontrolný súčet, tak sme ho zmazali. Zvyčajne pomôže skúsiť to znova.",
            true),

        ManifestException => new(
            "Nepodarilo sa prečítať informácie o verzii.",
            "Server odpovedal, ale niečím, čomu tento launcher nerozumie.",
            false),

        GameLaunchException e => new(
            "Hru sa nepodarilo spustiť.",
            e.Message + " Môže pomôcť oprava inštalácie.",
            true),

        LauncherUpdateException e => new(
            "Launcher sa nedokázal aktualizovať.",
            e.Message,
            false),

        UpdateException e => new(
            e.Message,
            null,
            true),

        HttpRequestException => new(
            "Nepodarilo sa spojiť so serverom.",
            "Skontroluj pripojenie a skús to znova.",
            true),

        OperationCanceledException => new(
            "Zrušené.",
            null,
            true),

        UnauthorizedAccessException => new(
            "Launcher nemá právo zapisovať tam, kam inštaluje.",
            "Skontroluj práva k priečinku, alebo spusti launcher z iného miesta.",
            false),

        IOException e => new(
            "Súbor sa nepodarilo zapísať.",
            e.Message,
            true),

        _ => new(
            "Niečo sa pokazilo.",
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
