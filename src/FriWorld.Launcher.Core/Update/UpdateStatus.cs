using System.Globalization;
using FriWorld.Launcher.Core.Localization;
using FriWorld.Launcher.Core.Net;

namespace FriWorld.Launcher.Core.Update;

public enum UpdateStage
{
    Idle,
    CheckingForUpdate,
    UpToDate,
    Downloading,
    Verifying,
    Extracting,
    Installing,
    Ready,
    Launching,
    Failed,
}

/// <summary>
/// One progress update. Everything a front end needs to render a state is here, so the view
/// never has to reach into the pipeline to work out what is happening.
///
/// It carries no sentence, only the stage and the data a sentence would need. The pipeline runs
/// under a window that may be speaking either language and under a console that is always
/// English; a phase name chosen here would be right for one of them and wrong for the other.
/// </summary>
/// <param name="Stage">Which phase of the one process this is.</param>
/// <param name="Version">The version this phase is about, when it is about one.</param>
/// <param name="Fraction">0 to 1, or null when the phase cannot say.</param>
/// <param name="Download">Present only while bytes are moving.</param>
public readonly record struct UpdateStatus(
    UpdateStage Stage,
    string? Version = null,
    double? Fraction = null,
    DownloadProgress? Download = null)
{
    public static UpdateStatus Of(UpdateStage stage, string? version = null) => new(stage, version);

    /// <summary>The phase named for a person, in the language they are being spoken to in.</summary>
    public string Message(Texts texts) => texts.Stage(Stage, Version);

    /// <summary>
    /// The line under the progress bar: how far along, how fast, how much longer.
    ///
    /// Empty for phases that move without a byte count — a spinning bar with no numbers says
    /// "working" honestly, where invented numbers would not.
    /// </summary>
    public string DetailLine(Texts texts) => Download is { } download
        ? Compose(download, texts)
        : string.Empty;

    private static string Compose(DownloadProgress download, Texts texts)
    {
        var line = texts.ReceivedOfTotal(download.BytesReceived, download.TotalBytes);

        return download.Remaining is { } left
            ? $"{line} · {texts.TimeLeft(left)}"
            : line;
    }

    /// <summary>Percent for the right-hand side of the phase row, or empty when unknown.</summary>
    public string PercentText => Fraction is { } fraction
        ? (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + " %"
        : string.Empty;
}
