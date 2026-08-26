using System.Globalization;
using FriWorld.Launcher.Core.Install;
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
/// </summary>
/// <param name="Stage">Which phase of the one process this is.</param>
/// <param name="Message">The phase named for a person, in Slovak.</param>
/// <param name="Fraction">0 to 1, or null when the phase cannot say.</param>
/// <param name="Download">Present only while bytes are moving.</param>
public readonly record struct UpdateStatus(
    UpdateStage Stage,
    string Message,
    double? Fraction = null,
    DownloadProgress? Download = null)
{
    public static UpdateStatus Of(UpdateStage stage, string message) => new(stage, message);

    /// <summary>
    /// The line under the progress bar: how far along, how fast, how much longer.
    ///
    /// Empty for phases that move without a byte count — a spinning bar with no numbers says
    /// "working" honestly, where invented numbers would not.
    /// </summary>
    public string DetailLine => Download is { } download
        ? Compose(download)
        : string.Empty;

    private static string Compose(DownloadProgress download)
    {
        var received = Format(download.BytesReceived);
        var total = download.TotalBytes is { } bytes ? Format(bytes) : "?";
        var line = $"{received} z {total}";

        return download.Remaining is { } left
            ? $"{line} · zostáva {left:mm\\:ss}"
            : line;
    }

    /// <summary>Slovak uses a decimal comma; a dot reads as a thousands separator here.</summary>
    private static string Format(long bytes) =>
        DiskSpace.Format(bytes).Replace('.', ',');

    /// <summary>Percent for the right-hand side of the phase row, or empty when unknown.</summary>
    public string PercentText => Fraction is { } fraction
        ? (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + " %"
        : string.Empty;
}
