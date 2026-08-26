namespace FriWorld.Launcher.Core.Net;

/// <summary>
/// Serves <c>file://</c> URLs. This is what stands in for remote object storage during development:
/// point the manifest at a local folder and the entire download-verify-extract-swap path runs for
/// real, with only the network absent.
/// </summary>
public sealed class FileContentClient : IContentClient
{
    private const int CopyBufferSize = 1024 * 1024;

    /// <summary>Set to a byte-per-second figure to make a local copy behave like a real download.</summary>
    public const string BandwidthVariable = "FRIWORLD_SIMULATED_BANDWIDTH";

    private readonly long _bytesPerSecond;

    /// <summary>
    /// A local copy finishes hundreds of megabytes in a couple of seconds, which makes the
    /// progress bar, the throughput figure and the cancel button impossible to look at. Giving
    /// the stand-in a speed limit makes it stand in for the thing it is replacing, rather than
    /// only for its result.
    /// </summary>
    /// <param name="bytesPerSecond">Zero or negative copies as fast as the disk allows.</param>
    public FileContentClient(long bytesPerSecond = 0)
    {
        _bytesPerSecond = bytesPerSecond > 0 ? bytesPerSecond : FromEnvironment();
    }

    private static long FromEnvironment() =>
        long.TryParse(Environment.GetEnvironmentVariable(BandwidthVariable), out var value) && value > 0
            ? value
            : 0;

    public bool CanHandle(Uri uri) => uri.IsFile;

    public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
        File.ReadAllTextAsync(uri.LocalPath, ct);

    public async Task DownloadToFileAsync(
        Uri uri,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var source = uri.LocalPath;

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Mock source file not found: {source}", source);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var total = new FileInfo(source).Length;
        var reporter = new ThrottledProgress(progress, total);

        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        await using var output = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);

        // Small chunks when throttled, so progress moves smoothly rather than in one-megabyte jumps.
        var chunk = _bytesPerSecond > 0
            ? (int)Math.Clamp(_bytesPerSecond / 20, 16 * 1024, CopyBufferSize)
            : CopyBufferSize;

        var buffer = new byte[chunk];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long copied = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            copied += read;
            reporter.Report(copied);

            await Throttle(copied, clock, ct).ConfigureAwait(false);
        }

        reporter.ReportFinal(copied);
    }

    /// <summary>
    /// Waits until the copy has taken as long as the configured speed says it should.
    ///
    /// Measured against the total elapsed time rather than per chunk, so a slow read does not
    /// add its own delay on top and the average comes out at the requested rate.
    /// </summary>
    private async Task Throttle(long copied, System.Diagnostics.Stopwatch clock, CancellationToken ct)
    {
        if (_bytesPerSecond <= 0)
        {
            return;
        }

        var owed = TimeSpan.FromSeconds((double)copied / _bytesPerSecond) - clock.Elapsed;

        if (owed > TimeSpan.Zero)
        {
            await Task.Delay(owed, ct).ConfigureAwait(false);
        }
    }
}
