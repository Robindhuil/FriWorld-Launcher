using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Tests;

public class InstallTests
{
    private static void Seed(string directory, string marker)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "marker.txt"), marker);
    }

    private static string MarkerOf(string directory) =>
        File.ReadAllText(Path.Combine(directory, "marker.txt"));

    [Fact]
    public void Promote_installs_when_nothing_was_there()
    {
        using var temp = new TempDirectory("first-install");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.GameNew, "v1");

        new AtomicInstaller(paths).Promote();

        Assert.Equal("v1", MarkerOf(paths.Game));
        Assert.False(Directory.Exists(paths.GameNew));
        Assert.False(Directory.Exists(paths.GameOld));
    }

    [Fact]
    public void Promote_keeps_the_previous_install_until_it_is_pruned()
    {
        using var temp = new TempDirectory("keep-old");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.Game, "v1");
        Seed(paths.GameNew, "v2");

        new AtomicInstaller(paths).Promote();

        Assert.Equal("v2", MarkerOf(paths.Game));
        Assert.Equal("v1", MarkerOf(paths.GameOld));
    }

    [Fact]
    public void Promote_clears_a_stale_old_install_left_by_an_earlier_run()
    {
        using var temp = new TempDirectory("stale-old");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.GameOld, "v0");
        Seed(paths.Game, "v1");
        Seed(paths.GameNew, "v2");

        new AtomicInstaller(paths).Promote();

        Assert.Equal("v1", MarkerOf(paths.GameOld));
    }

    [Fact]
    public void Rollback_restores_the_previous_install()
    {
        using var temp = new TempDirectory("rollback");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.Game, "v1");
        Seed(paths.GameNew, "v2-broken");

        var installer = new AtomicInstaller(paths);
        installer.Promote();

        Assert.True(installer.Rollback());
        Assert.Equal("v1", MarkerOf(paths.Game));
    }

    [Fact]
    public void Rollback_reports_failure_when_there_is_nothing_to_restore()
    {
        using var temp = new TempDirectory("no-rollback");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.GameNew, "v1");

        var installer = new AtomicInstaller(paths);
        installer.Promote();

        Assert.False(installer.Rollback());
    }

    [Fact]
    public void PruneOldInstall_removes_the_previous_install()
    {
        using var temp = new TempDirectory("prune");
        var paths = new LauncherPaths(temp.Path);
        Seed(paths.Game, "v1");
        Seed(paths.GameNew, "v2");

        var installer = new AtomicInstaller(paths);
        installer.Promote();
        installer.PruneOldInstall();

        Assert.False(Directory.Exists(paths.GameOld));
    }

    [Fact]
    public void Installed_state_round_trips()
    {
        using var temp = new TempDirectory("state");
        var store = new InstalledStateStore(new LauncherPaths(temp.Path));

        var written = new InstalledState
        {
            Version = "0.1.2-alpha",
            Platform = PlatformKey.WindowsX64,
            InstalledAt = DateTimeOffset.UtcNow,
            Sha256 = new string('a', 64),
            Exec = "FriWorld.exe",
            LaunchConfirmed = true,
        };

        store.Write(written);
        var read = store.Read();

        Assert.NotNull(read);
        Assert.Equal(written.Version, read.Version);
        Assert.Equal(written.Exec, read.Exec);
        Assert.True(read.LaunchConfirmed);
    }

    [Fact]
    public void A_corrupt_state_file_reads_as_nothing_installed()
    {
        using var temp = new TempDirectory("corrupt-state");
        var paths = new LauncherPaths(temp.Path);
        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(paths.InstalledStateFile, "{ this is not json");

        Assert.Null(new InstalledStateStore(paths).Read());
    }

    [Fact]
    public void Free_space_is_required_for_three_copies_plus_a_margin()
    {
        // Archive in the cache, extracted tree in game.new, and the previous install in game.old.
        const long archive = 1_000_000_000;

        Assert.True(DiskSpace.RequiredBytes(archive) > archive * 3);
    }

    [Fact]
    public void The_second_launcher_cannot_take_the_lock()
    {
        using var temp = new TempDirectory("lock");
        var paths = new LauncherPaths(temp.Path);

        using var first = SingleInstanceLock.TryAcquire(paths);
        Assert.NotNull(first);

        using var second = SingleInstanceLock.TryAcquire(paths);
        Assert.Null(second);
    }

    [Fact]
    public void The_lock_is_released_when_the_launcher_exits()
    {
        using var temp = new TempDirectory("lock-release");
        var paths = new LauncherPaths(temp.Path);

        SingleInstanceLock.TryAcquire(paths)!.Dispose();

        using var again = SingleInstanceLock.TryAcquire(paths);
        Assert.NotNull(again);
    }

    [Fact]
    public void The_install_root_can_be_redirected()
    {
        using var temp = new TempDirectory("root");
        var paths = new LauncherPaths(temp.Path);

        Assert.Equal(Path.Combine(paths.Root, "game"), paths.Game);
        Assert.Equal(Path.Combine(paths.Root, "installed.json"), paths.InstalledStateFile);
    }
}
