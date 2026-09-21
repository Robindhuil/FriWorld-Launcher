using System.Globalization;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Localization;

/// <summary>
/// Every sentence the launcher says to a player, in both languages.
///
/// The two translations sit on the same line, in the same member, on purpose. A resource file per
/// language is the usual shape and it has one flaw that matters more than everything it offers:
/// the languages drift, because nothing forces the second file to keep up with the first, and the
/// drift is invisible until someone runs the launcher in the language nobody tests. Here a string
/// that exists in one language exists in both or the file does not compile.
///
/// It also keeps the window honest about the other half of a translation. A sentence is not
/// translated when its numbers still read as Slovak — <c>1,5 GB</c> is right here and wrong in
/// English — so the decimal separator travels with the words rather than being applied somewhere
/// further down.
///
/// The separators are written out rather than taken from a culture because the whole solution
/// builds with <c>InvariantGlobalization</c>: culture lookup returns the invariant culture on
/// every machine, so asking for <c>sk-SK</c> would silently give English formatting. Two explicit
/// separators are also the same on a school computer as on a developer's, which a culture
/// database is not.
///
/// What is not here: the log and the CLI's own output. Those are read by whoever is fixing
/// something, they stay English everywhere, and mixing them in would mean translating text no
/// player ever sees.
/// </summary>
public sealed class Texts
{
    public static readonly Texts Slovak = new(Language.Slovak);

    public static readonly Texts English = new(Language.English);

    private readonly bool _en;

    private Texts(Language language)
    {
        Language = language;
        _en = language == Language.English;

        var numbers = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        numbers.NumberDecimalSeparator = _en ? "." : ",";
        NumberFormat = numbers;
    }

    public static Texts For(Language language) => language == Language.English ? English : Slovak;

    public Language Language { get; }

    /// <summary>Decimal separator and friends, for every number a player reads.</summary>
    public NumberFormatInfo NumberFormat { get; }

    /// <summary>A size a player reads, with this language's decimal separator.</summary>
    public string Size(long bytes) => DiskSpace.Format(bytes, NumberFormat);

    private string Pick(string sk, string en) => _en ? en : sk;

    // ---- The window's chrome -------------------------------------------------------------

    public string LauncherActions => Pick("Akcie launchera", "Launcher actions");

    public string CheckAgain => Pick("Skontrolovať znova", "Check again");

    public string OpenTheLog => Pick("Otvoriť denník launchera", "Open the launcher log");

    /// <summary>
    /// Names the language switch in the title bar. What is inside it is not translated: the two
    /// languages carry their own names, from <see cref="Languages.NativeName"/>. A list that
    /// offered "Angličtina" would be unreadable to exactly the person who has to find it.
    /// </summary>
    public string LanguageMenu => Pick("Jazyk", "Language");

    public string Minimise => Pick("Minimalizovať", "Minimise");

    public string Close => Pick("Zavrieť", "Close");

    public string Cancel => Pick("Zrušiť", "Cancel");

    public string WhatIsNew => Pick("ČO JE NOVÉ", "WHAT'S NEW");

    public string ErrorLabel => Pick("CHYBA", "ERROR");

    public string Uninstall => Pick("Odinštalovať", "Uninstall");

    public string RepairTheInstallation => Pick("Opraviť inštaláciu", "Repair the installation");

    public string OpenTheGameFolder => Pick("Otvoriť priečinok s hrou", "Open the game folder");

    // ---- Buttons that act on the game ----------------------------------------------------

    public string Install => Pick("Inštalovať", "Install");

    public string Update => Pick("Aktualizovať", "Update");

    public string Play => Pick("Hrať", "Play");

    public string TryAgain => Pick("Skúsiť znova", "Try again");

    public string JustAMoment => Pick("Počkaj chvíľu", "Just a moment");

    public string PlayVersion(string? version) => Pick($"Hrať {version}", $"Play {version}");

    // ---- Version line and the states it describes -----------------------------------------

    public string Version(string? version) => Pick($"Verzia {version}", $"Version {version}");

    public string VersionInstalled(string? version) =>
        Pick($"Verzia {version} nainštalovaná", $"Version {version} installed");

    public string VersionAvailable(string? version) =>
        Pick($"Verzia {version} k dispozícii", $"Version {version} available");

    public string InstalledAndAvailable(string? installed, string? latest) => Pick(
        $"Verzia {installed} nainštalovaná · {latest} k dispozícii",
        $"Version {installed} installed · {latest} available");

    public string Starting => Pick("Spúšťam", "Starting");

    public string Ready => Pick("Pripravené", "Ready");

    public string NotInstalled => Pick("Nenainštalované", "Not installed");

    public string UpdateToVersion(string? version) =>
        Pick($"Aktualizovať na {version}?", $"Update to {version}?");

    public string CanPlayMeanwhile(string? version) =>
        Pick($"Zatiaľ môžeš hrať {version}.", $"You can play {version} in the meantime.");

    public string ToDownload(long bytes) =>
        Pick($"Na stiahnutie {Size(bytes)}.", $"{Size(bytes)} to download.");

    public string Running => Pick("Beží", "Running");

    public string RepairingTheInstallation => Pick("Opravujem inštaláciu", "Repairing the installation");

    public string RepairedAndReady => Pick("Opravené a pripravené", "Repaired and ready");

    public string StartingTheGame => Pick("Spúšťam hru", "Starting the game");

    public string Cancelling => Pick("Ruším", "Cancelling");

    public string Cancelled => Pick("Zrušené", "Cancelled");

    public string PartialDownloadKept => Pick(
        "Čiastočne stiahnuté súbory sme nechali, nabudúce sa bude pokračovať tam, kde si prestal.",
        "The partly downloaded files were kept; next time it carries on where it stopped.");

    public string CheckingForUpdates => Pick("Kontrolujem aktualizácie", "Checking for updates");

    public string AskingTheServer => Pick("Zisťujem, čo je na serveri.", "Asking the server what is there.");

    public string Uninstalled => Pick("Odinštalované", "Uninstalled");

    public string TheGameWasRemoved => Pick("Hra bola odstránená.", "The game has been removed.");

    public string TheGameWasRemovedAndCanBeDownloaded(long bytes) => Pick(
        $"Hra bola odstránená. Na stiahnutie {Size(bytes)}.",
        $"The game has been removed. {Size(bytes)} to download.");

    // ---- Progress ------------------------------------------------------------------------

    /// <summary>The phase name beside the progress bar. Every stage names itself here, once.</summary>
    public string Stage(UpdateStage stage, string? version = null) => stage switch
    {
        UpdateStage.CheckingForUpdate => CheckingForUpdates,
        UpdateStage.UpToDate => Pick($"Máš najnovšiu verziu {version}", $"You have the latest version, {version}"),
        UpdateStage.Downloading => Pick("Sťahujem", "Downloading"),
        UpdateStage.Verifying => VerifyingTheDownload,
        UpdateStage.Extracting => Pick("Rozbaľujem", "Extracting"),
        UpdateStage.Installing => Pick("Inštalujem", "Installing"),
        UpdateStage.Ready => Ready,
        UpdateStage.Launching => StartingTheGame,
        UpdateStage.Failed => Pick("Nepodarilo sa", "Failed"),
        _ => string.Empty,
    };

    public string VerifyingTheDownload => Pick("Overujem stiahnuté", "Verifying the download");

    public string DownloadingVersion(string? version) =>
        Pick($"Sťahujem {version}", $"Downloading {version}");

    public string InstallingVersion(string? version) =>
        Pick($"Inštalujem {version}", $"Installing {version}");

    public string DownloadingTheNewLauncher => Pick("Sťahujem nový launcher", "Downloading the new launcher");

    public string Restarting => Pick("Reštartujem", "Restarting");

    public string ReceivedOfTotal(long received, long? total) => Pick(
        $"{Size(received)} z {(total is { } sk ? Size(sk) : "?")}",
        $"{Size(received)} of {(total is { } en ? Size(en) : "?")}");

    public string TimeLeft(TimeSpan remaining) => Pick(
        $"zostáva {remaining:mm\\:ss}",
        $"{remaining:mm\\:ss} left");

    // ---- Questions -----------------------------------------------------------------------

    public string UninstallTheGame => Pick("Odinštalovať hru?", "Uninstall the game?");

    public string CloseTheLauncher => Pick("Zavrieť launcher?", "Close the launcher?");

    public string UninstallQuestionDetail => Pick(
        "Stiahnuté súbory hry sa vymažú. Vrátiť sa to nedá, ale hru sa dá kedykoľvek nainštalovať znova.",
        "The downloaded game files will be deleted. That cannot be undone, but the game can be installed again at any time.");

    public string ClosingStopsTheDownload => Pick(
        "Sťahovanie sa zastaví. Stiahnuté súbory zostanú a nabudúce sa bude pokračovať tam, kde prestalo.",
        "The download will stop. What has been downloaded is kept, and next time it carries on where it stopped.");

    public string ClosingLosesWorkInProgress => Pick(
        "Launcher práve pracuje. Zavretie teraz nechá rozrobenú prácu, ktorú bude treba spraviť znova.",
        "The launcher is busy. Closing now leaves work half done, and it will have to be done again.");

    public string TheGameStaysInstalled => Pick("Hra zostane nainštalovaná.", "The game stays installed.");

    public string Keep => Pick("Ponechať", "Keep");

    public string Back => Pick("Späť", "Back");

    // ---- The launcher updating itself ------------------------------------------------------

    public string LauncherAvailable(string? version) =>
        Pick($"K dispozícii je launcher {version}.", $"Launcher {version} is available.");

    public string UpdateAndRestart => Pick("Aktualizovať a reštartovať", "Update and restart");

    public string OpenTheDownloadPage => Pick("Otvoriť stránku so stiahnutím", "Open the download page");

    public string BrowserWouldNotOpen(string page) => Pick(
        $"Prehliadač sa nepodarilo otvoriť. Stránka je {page}",
        $"The browser would not open. The page is {page}");

    // ---- Failures --------------------------------------------------------------------------

    public string AnotherLauncherIsRunning => Pick("Už beží iný launcher.", "Another launcher is already running.");

    public string GameIsRunningHeadline => Pick("Hra už beží.", "The game is already running.");

    public string GameIsRunningAdvice => Pick("Zavri ju a skús to znova.", "Close it and try again.");

    public string LauncherTooOldHeadline => Pick("Tento launcher je príliš starý.", "This launcher is too old.");

    public string LauncherTooOldAdvice(string? required, string? current) => (required, current) switch
    {
        (not null, not null) => Pick(
            $"Toto vydanie potrebuje launcher {required} alebo novší, tento je {current}. Stiahni si novší.",
            $"This release needs launcher {required} or newer; this one is {current}. Download a newer one."),
        _ => Pick("Stiahni si novší.", "Download a newer one."),
    };

    /// <summary>The launcher is too old for the release on the server, said in the window's own words.</summary>
    public string ReleaseNeedsNewerLauncher(string? release, string? minimum) => Pick(
        $"Vydanie {release} potrebuje launcher {minimum} alebo novší.",
        $"Release {release} needs launcher {minimum} or newer.");

    public string LauncherTooOldStatus => Pick("Launcher je príliš starý", "The launcher is too old");

    public string NotEnoughSpaceHeadline => Pick("Nedostatok voľného miesta.", "Not enough free space.");

    public string NotEnoughSpaceAdvice(long required, long available, string? drive) =>
        required > 0
            ? Pick(
                $"Treba asi {Size(required)} na {drive}, voľných je {Size(available)}. " +
                "Miesto treba na stiahnutie aj rozbalenie naraz.",
                $"About {Size(required)} is needed on {drive}, but {Size(available)} is free. " +
                "The space is needed for the download and the unpacking at the same time.")
            : Pick(
                "Miesto treba na stiahnutie aj rozbalenie naraz.",
                "The space is needed for the download and the unpacking at the same time.");

    public string CorruptedDownloadHeadline => Pick("Stiahnutý súbor je poškodený.", "The downloaded file is damaged.");

    public string CorruptedDownloadAdvice => Pick(
        "Nesedel kontrolný súčet, tak sme ho zmazali. Zvyčajne pomôže skúsiť to znova.",
        "Its checksum did not match, so it was deleted. Trying again usually helps.");

    public string ManifestUnreadableHeadline => Pick(
        "Nepodarilo sa prečítať informácie o verzii.",
        "The version information could not be read.");

    public string ManifestUnreadableAdvice => Pick(
        "Server odpovedal, ale niečím, čomu tento launcher nerozumie.",
        "The server answered, but with something this launcher does not understand.");

    public string GameWouldNotStartHeadline => Pick("Hru sa nepodarilo spustiť.", "The game would not start.");

    public string GameWouldNotStartAdvice(GameLaunchProblem problem, string? path)
    {
        var repair = Pick("Môže pomôcť oprava inštalácie.", "Repairing the installation may help.");

        return problem switch
        {
            GameLaunchProblem.ExecutableNotNamed => Pick(
                $"Informácie o verzii nehovoria, ktorý súbor spustiť. {repair}",
                $"The version information does not say which file to run. {repair}"),

            GameLaunchProblem.ExecutableMissing => Pick(
                $"Súbor {path} v inštalácii chýba. {repair}",
                $"The file {path} is missing from the installation. {repair}"),

            GameLaunchProblem.SystemRefused => Pick(
                $"Systém odmietol spustiť {path}. {repair}",
                $"The system refused to start {path}. {repair}"),

            _ => repair,
        };
    }

    public string LauncherUpdateFailedHeadline => Pick(
        "Launcher sa nedokázal aktualizovať.",
        "The launcher could not update itself.");

    public string LauncherUpdateFailedAdvice(LauncherUpdateProblem problem, string? path) => problem switch
    {
        LauncherUpdateProblem.BinaryNotOffered => Pick(
            "Informácie o verzii nenesú launcher pre tento počítač. Stiahni si ho zo stránky.",
            "The version information carries no launcher for this computer. Download it from the page."),

        LauncherUpdateProblem.NotASingleFileBuild => Pick(
            "Tento launcher nie je jednosúborová zostava, takže sa nevie nahradiť sám. Stiahni si nový zo stránky.",
            "This launcher is not a single-file build, so it cannot replace itself. Download a new one from the page."),

        LauncherUpdateProblem.DirectoryNotWritable => Pick(
            $"Do priečinka {path} sa nedá zapisovať. Skús launcher presunúť inam, napríklad na plochu.",
            $"The folder {path} cannot be written to. Try moving the launcher elsewhere, to the desktop for instance."),

        LauncherUpdateProblem.NotRestored => Pick(
            $"Aktualizácia zlyhala a pôvodný launcher sa nepodarilo vrátiť. Premenuj {path} späť.",
            $"The update failed and the original launcher could not be put back. Rename {path} back."),

        _ => Pick(
            "Skús to znova, alebo si stiahni nový launcher zo stránky.",
            "Try again, or download a new launcher from the page."),
    };

    public string CouldNotReachTheServerHeadline => Pick(
        "Nepodarilo sa spojiť so serverom.",
        "The server could not be reached.");

    public string CouldNotReachTheServerAdvice => Pick(
        "Skontroluj pripojenie a skús to znova.",
        "Check the connection and try again.");

    public string NoWritePermissionHeadline => Pick(
        "Launcher nemá právo zapisovať tam, kam inštaluje.",
        "The launcher is not allowed to write where it installs.");

    public string NoWritePermissionAdvice => Pick(
        "Skontroluj práva k priečinku, alebo spusti launcher z iného miesta.",
        "Check the folder's permissions, or run the launcher from somewhere else.");

    public string FileNotWrittenHeadline => Pick("Súbor sa nepodarilo zapísať.", "A file could not be written.");

    public string SomethingWentWrongHeadline => Pick("Niečo sa pokazilo.", "Something went wrong.");

    public string TryAgainAdvice => Pick("Skús to znova.", "Try again.");

    public string NoBuildForThisComputer => Pick(
        "Toto vydanie nemá zostavu pre tento počítač.",
        "This release has no build for this computer.");

    public string GameClosedImmediatelyHeadline => Pick("Hra sa hneď zavrela.", "The game closed immediately.");

    public string GameClosedImmediatelyAdvice(int exitCode) => Pick(
        $"Skončila s kódom {exitCode} pár sekúnd po spustení. Môže pomôcť oprava inštalácie.",
        $"It exited with code {exitCode} a few seconds after starting. Repairing the installation may help.");

    public string GameFolderWouldNotOpen => Pick(
        "Priečinok s hrou sa nepodarilo otvoriť.",
        "The game folder would not open.");

    public string LogWouldNotOpen => Pick(
        "Denník launchera sa nepodarilo otvoriť.",
        "The launcher log would not open.");
}
