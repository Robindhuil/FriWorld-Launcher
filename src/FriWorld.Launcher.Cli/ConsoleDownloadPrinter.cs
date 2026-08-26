using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Net;

namespace FriWorld.Launcher.Cli;

/// <summary>
/// Renders a bare download as a single rewriting line, for the launcher's own update where there
/// are no pipeline stages to report.
/// </summary>
public sealed class ConsoleDownloadPrinter : IProgress<DownloadProgress>
{
    private readonly Lock _gate = new();
    private int _lastLineLength;

    public void Report(DownloadProgress value)
    {
        lock (_gate)
        {
            var received = DiskSpace.Format(value.BytesReceived);
            var total = value.TotalBytes is { } bytes ? DiskSpace.Format(bytes) : "?";
            var line = $"Downloading launcher  {received} / {total}";

            Console.Write('\r' + line + new string(' ', Math.Max(0, _lastLineLength - line.Length)));
            _lastLineLength = line.Length;

            if (value.Fraction >= 1)
            {
                Console.WriteLine();
                _lastLineLength = 0;
            }
        }
    }
}
