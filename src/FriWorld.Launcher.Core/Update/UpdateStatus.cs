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
/// One progress update. Everything the UI needs to render a state is here, so the view never has
/// to reach into the pipeline to work out what is happening.
/// </summary>
public readonly record struct UpdateStatus(
    UpdateStage Stage,
    string Message,
    double? Fraction = null,
    DownloadProgress? Download = null)
{
    public static UpdateStatus Of(UpdateStage stage, string message) => new(stage, message);
}
