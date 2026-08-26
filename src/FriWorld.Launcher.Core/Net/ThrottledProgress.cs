using System.Diagnostics;

namespace FriWorld.Launcher.Core.Net;

/// <summary>
/// Rate-limits progress callbacks and estimates throughput. Without this a 1 GB download would
/// raise hundreds of thousands of UI updates and spend more time repainting than copying.
/// </summary>
internal sealed class ThrottledProgress(IProgress<DownloadProgress>? inner, long? total, int intervalMs = 100)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastReportedTicks;
    private long _lastBytes;
    private double _speed;

    public void Report(long bytesReceived)
    {
        if (inner is null)
        {
            return;
        }

        var now = _clock.ElapsedMilliseconds;
        var sinceLast = now - _lastReportedTicks;

        if (sinceLast < intervalMs)
        {
            return;
        }

        var delta = bytesReceived - _lastBytes;
        var instant = delta / (sinceLast / 1000d);

        // Exponential smoothing keeps the number readable instead of jittering every tick.
        _speed = _speed <= 0 ? instant : (_speed * 0.7) + (instant * 0.3);

        _lastReportedTicks = now;
        _lastBytes = bytesReceived;

        inner.Report(new DownloadProgress(bytesReceived, total, _speed));
    }

    public void ReportFinal(long bytesReceived) =>
        inner?.Report(new DownloadProgress(bytesReceived, total ?? bytesReceived, _speed));
}
