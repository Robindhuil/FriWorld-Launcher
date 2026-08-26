using System.Diagnostics;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The lock that keeps two launchers from downloading into the same folder — and, just as
/// importantly, that lets a launcher hand over to its own replacement.
/// </summary>
public class SingleInstanceLockTests
{
    [Fact]
    public void The_second_launcher_is_refused()
    {
        using var temp = new TempDirectory("lock-second");
        var paths = new LauncherPaths(temp.Path);

        using var first = SingleInstanceLock.TryAcquire(paths);
        Assert.NotNull(first);

        // Short wait: the point here is that it is refused, not how patiently.
        using var second = SingleInstanceLock.TryAcquire(paths, TimeSpan.Zero);
        Assert.Null(second);
    }

    [Fact]
    public void Refusing_does_not_take_longer_than_asked()
    {
        using var temp = new TempDirectory("lock-timeout");
        var paths = new LauncherPaths(temp.Path);

        using var first = SingleInstanceLock.TryAcquire(paths);
        Assert.NotNull(first);

        var clock = Stopwatch.StartNew();
        using var second = SingleInstanceLock.TryAcquire(paths, TimeSpan.FromMilliseconds(300));
        clock.Stop();

        Assert.Null(second);
        Assert.InRange(clock.ElapsedMilliseconds, 250, 3000);
    }

    [Fact]
    public async Task A_lock_released_mid_wait_is_picked_up()
    {
        // This is the self-update handover: the outgoing launcher lets go a moment after its
        // replacement has already started asking. Without the wait the new one would report
        // that another launcher is running, and the update would look like it broke everything.
        using var temp = new TempDirectory("lock-handover");
        var paths = new LauncherPaths(temp.Path);

        var outgoing = SingleInstanceLock.TryAcquire(paths);
        Assert.NotNull(outgoing);

        var releasing = Task.Run(async () =>
        {
            await Task.Delay(250);
            outgoing.Dispose();
        });

        using var incoming = SingleInstanceLock.TryAcquire(paths, TimeSpan.FromSeconds(5));

        Assert.NotNull(incoming);
        await releasing;
    }

    [Fact]
    public void The_lock_is_released_when_the_launcher_exits()
    {
        using var temp = new TempDirectory("lock-release");
        var paths = new LauncherPaths(temp.Path);

        SingleInstanceLock.TryAcquire(paths)!.Dispose();

        using var again = SingleInstanceLock.TryAcquire(paths, TimeSpan.Zero);
        Assert.NotNull(again);
    }
}
