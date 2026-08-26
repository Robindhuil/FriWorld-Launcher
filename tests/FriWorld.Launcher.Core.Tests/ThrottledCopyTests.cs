using System.Diagnostics;
using FriWorld.Launcher.Core.Net;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The speed limit on the local content client. It exists so the progress bar, the throughput
/// figure and the cancel button can be looked at against a local folder, where a copy would
/// otherwise finish before anything renders.
/// </summary>
public class ThrottledCopyTests
{
    private static async Task<string> SourceFile(TempDirectory temp, int bytes)
    {
        var path = temp.Combine("source.bin");
        await File.WriteAllBytesAsync(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public async Task Without_a_limit_the_copy_is_not_slowed()
    {
        using var temp = new TempDirectory("throttle-off");
        var source = await SourceFile(temp, 512 * 1024);

        var clock = Stopwatch.StartNew();
        await new FileContentClient().DownloadToFileAsync(
            new Uri(source), temp.Combine("out.bin"), null, null, CancellationToken.None);
        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 2000, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task A_limit_makes_the_copy_take_roughly_the_expected_time()
    {
        using var temp = new TempDirectory("throttle-on");
        var source = await SourceFile(temp, 400 * 1024);

        // 400 KB at 400 KB/s is about a second.
        var clock = Stopwatch.StartNew();
        await new FileContentClient(400 * 1024).DownloadToFileAsync(
            new Uri(source), temp.Combine("out.bin"), null, null, CancellationToken.None);
        clock.Stop();

        Assert.InRange(clock.ElapsedMilliseconds, 700, 4000);
    }

    [Fact]
    public async Task A_throttled_copy_still_produces_the_whole_file()
    {
        using var temp = new TempDirectory("throttle-content");
        var source = temp.Combine("source.bin");
        var payload = new byte[200 * 1024];
        Random.Shared.NextBytes(payload);
        await File.WriteAllBytesAsync(source, payload);

        var destination = temp.Combine("out.bin");
        await new FileContentClient(1024 * 1024).DownloadToFileAsync(
            new Uri(source), destination, null, null, CancellationToken.None);

        Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task A_throttled_copy_can_be_cancelled_part_way()
    {
        // This is the point of the limit: it makes the cancel button reachable.
        using var temp = new TempDirectory("throttle-cancel");
        var source = await SourceFile(temp, 2 * 1024 * 1024);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileContentClient(256 * 1024).DownloadToFileAsync(
                new Uri(source), temp.Combine("out.bin"), null, null, cancellation.Token));
    }

    [Fact]
    public async Task Progress_is_reported_more_than_once_when_throttled()
    {
        using var temp = new TempDirectory("throttle-progress");
        var source = await SourceFile(temp, 600 * 1024);

        var reports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p =>
        {
            lock (reports)
            {
                reports.Add(p);
            }
        });

        await new FileContentClient(300 * 1024).DownloadToFileAsync(
            new Uri(source), temp.Combine("out.bin"), null, progress, CancellationToken.None);

        // Progress<T> posts asynchronously, so give the callbacks a moment to land.
        await Task.Delay(300);

        lock (reports)
        {
            Assert.True(reports.Count > 1, $"only {reports.Count} progress report(s)");
        }
    }
}
