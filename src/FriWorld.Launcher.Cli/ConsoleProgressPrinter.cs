using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Cli;

/// <summary>Renders <see cref="UpdateStatus"/> as a single rewriting console line.</summary>
public sealed class ConsoleProgressPrinter : IProgress<UpdateStatus>
{
    private readonly Lock _gate = new();
    private UpdateStage _lastStage = UpdateStage.Idle;
    private int _lastLineLength;

    public void Report(UpdateStatus status)
    {
        lock (_gate)
        {
            // A new stage gets its own line; progress within a stage rewrites in place.
            if (status.Stage != _lastStage && _lastLineLength > 0)
            {
                Console.WriteLine();
                _lastLineLength = 0;
            }

            _lastStage = status.Stage;

            var line = Compose(status);
            var padding = Math.Max(0, _lastLineLength - line.Length);

            Console.Write('\r' + line + new string(' ', padding));
            _lastLineLength = line.Length;

            // Launching ends the line as well, otherwise the game's own output starts halfway
            // along ours: the launcher writes "Starting …" and the process prints its first line
            // before anything has moved to the next row.
            if (status.Stage is UpdateStage.Ready or UpdateStage.UpToDate
                or UpdateStage.Failed or UpdateStage.Launching)
            {
                Console.WriteLine();
                _lastLineLength = 0;
            }
        }
    }

    private static string Compose(UpdateStatus status)
    {
        var text = status.Message;

        if (status.Download is { } download)
        {
            var received = DiskSpace.Format(download.BytesReceived);
            var total = download.TotalBytes is { } t ? DiskSpace.Format(t) : "?";
            var speed = download.BytesPerSecond > 0
                ? $" at {DiskSpace.Format((long)download.BytesPerSecond)}/s"
                : string.Empty;
            var eta = download.Remaining is { } remaining
                ? $", {remaining:mm\\:ss} left"
                : string.Empty;

            return $"{text}  {Bar(status.Fraction)} {received} / {total}{speed}{eta}";
        }

        return status.Fraction is { } fraction
            ? $"{text}  {Bar(fraction)}"
            : text;
    }

    private static string Bar(double? fraction)
    {
        if (fraction is not { } value)
        {
            return string.Empty;
        }

        const int width = 24;
        var filled = (int)Math.Round(value * width);
        return $"[{new string('#', filled)}{new string('.', width - filled)}] {value * 100,5:0.0}%";
    }
}
