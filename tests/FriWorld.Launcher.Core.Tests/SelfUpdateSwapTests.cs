using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The swap itself, carried out on real files.
///
/// Everything else in the project can be re-run if it goes wrong. This cannot: it replaces the
/// program that would otherwise be able to fix things. So these tests do not check that the happy
/// path works — they check what is left behind when each step fails.
/// </summary>
public class SelfUpdateSwapTests
{
    private static LauncherSelfUpdater UpdaterFor(string executablePath, bool singleFile = true) =>
        new(CompositeContentClient.CreateDefault(),
            null,
            new LauncherDeployment(executablePath, singleFile));

    private static string Deploy(TempDirectory temp, string name, string contents)
    {
        var path = temp.Combine(name);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void The_new_launcher_takes_the_old_one_s_place()
    {
        using var temp = new TempDirectory("swap-ok");
        var running = Deploy(temp, "FriWorldLauncher.exe", "stará verzia");
        var staged = Deploy(temp, "FriWorldLauncher.exe.incoming", "nová verzia");

        UpdaterFor(running).Apply(staged, restart: false);

        Assert.Equal("nová verzia", File.ReadAllText(running));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void The_old_launcher_is_kept_aside_rather_than_deleted()
    {
        // A running executable cannot be overwritten but can be renamed, and this process is
        // still executing from it. Deleting it now would pull the floor out.
        using var temp = new TempDirectory("swap-keep");
        var running = Deploy(temp, "FriWorldLauncher.exe", "stará verzia");
        var staged = Deploy(temp, "FriWorldLauncher.exe.incoming", "nová verzia");

        UpdaterFor(running).Apply(staged, restart: false);

        var superseded = running + LauncherSelfUpdater.SupersededSuffix;
        Assert.True(File.Exists(superseded));
        Assert.Equal("stará verzia", File.ReadAllText(superseded));
    }

    [Fact]
    public void The_next_start_clears_what_the_last_update_left()
    {
        using var temp = new TempDirectory("swap-cleanup");
        var running = Deploy(temp, "FriWorldLauncher.exe", "nová verzia");
        var superseded = Deploy(temp, "FriWorldLauncher.exe" + LauncherSelfUpdater.SupersededSuffix, "stará");

        UpdaterFor(running).CleanUpSupersededExecutable();

        Assert.False(File.Exists(superseded));
        Assert.True(File.Exists(running));
    }

    [Fact]
    public void A_leftover_from_a_previous_attempt_does_not_block_the_next_one()
    {
        using var temp = new TempDirectory("swap-leftover");
        var running = Deploy(temp, "FriWorldLauncher.exe", "verzia 2");
        Deploy(temp, "FriWorldLauncher.exe" + LauncherSelfUpdater.SupersededSuffix, "verzia 1");
        var staged = Deploy(temp, "FriWorldLauncher.exe.incoming", "verzia 3");

        UpdaterFor(running).Apply(staged, restart: false);

        Assert.Equal("verzia 3", File.ReadAllText(running));
        Assert.Equal("verzia 2", File.ReadAllText(running + LauncherSelfUpdater.SupersededSuffix));
    }

    [WindowsOnlyFact]
    public void A_failed_swap_puts_the_running_launcher_back()
    {
        // The dangerous window: the old executable has been renamed aside and the new one cannot
        // be moved into place. Something must put the old one back, or there is no launcher.
        using var temp = new TempDirectory("swap-rollback");
        var running = Deploy(temp, "FriWorldLauncher.exe", "stará verzia");
        var staged = Deploy(temp, "FriWorldLauncher.exe.incoming", "nová verzia");

        // Hold the staged file open so it cannot be moved.
        using (new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<LauncherUpdateException>(() => UpdaterFor(running).Apply(staged, restart: false));
        }

        Assert.True(File.Exists(running));
        Assert.Equal("stará verzia", File.ReadAllText(running));
        Assert.False(File.Exists(running + LauncherSelfUpdater.SupersededSuffix));
    }

    [Fact]
    public void A_missing_staged_file_is_refused_before_anything_moves()
    {
        using var temp = new TempDirectory("swap-nostage");
        var running = Deploy(temp, "FriWorldLauncher.exe", "stará verzia");

        var error = Assert.Throws<LauncherUpdateException>(
            () => UpdaterFor(running).Apply(temp.Combine("nikde.incoming"), restart: false));

        Assert.Contains("missing", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("stará verzia", File.ReadAllText(running));
        Assert.False(File.Exists(running + LauncherSelfUpdater.SupersededSuffix));
    }

    [Fact]
    public void A_multi_file_deployment_refuses_to_swap()
    {
        // Half a launcher is worse than an old one, and a build spread over dozens of files
        // cannot be replaced in one move.
        using var temp = new TempDirectory("swap-multifile");
        var running = Deploy(temp, "FriWorldLauncher.exe", "stará verzia");
        var staged = Deploy(temp, "FriWorldLauncher.exe.incoming", "nová verzia");

        var updater = UpdaterFor(running, singleFile: false);

        Assert.NotNull(updater.BlockedReason());
        Assert.Throws<LauncherUpdateException>(() => updater.Apply(staged, restart: false));

        Assert.Equal("stará verzia", File.ReadAllText(running));
        Assert.True(File.Exists(staged));
    }

    [Fact]
    public void An_unknown_executable_path_refuses_to_swap()
    {
        using var temp = new TempDirectory("swap-nopath");
        var staged = Deploy(temp, "staged", "nová verzia");

        var updater = new LauncherSelfUpdater(
            CompositeContentClient.CreateDefault(), null, new LauncherDeployment(null, true));

        Assert.NotNull(updater.BlockedReason());
        Assert.Throws<LauncherUpdateException>(() => updater.Apply(staged, restart: false));
    }

    [Fact]
    public void Two_updates_in_a_row_leave_only_the_previous_one_behind()
    {
        using var temp = new TempDirectory("swap-twice");
        var running = Deploy(temp, "FriWorldLauncher.exe", "verzia 1");
        var updater = UpdaterFor(running);

        UpdaterFor(running).Apply(Deploy(temp, "FriWorldLauncher.exe.incoming", "verzia 2"), restart: false);
        updater.CleanUpSupersededExecutable();
        UpdaterFor(running).Apply(Deploy(temp, "FriWorldLauncher.exe.incoming", "verzia 3"), restart: false);

        Assert.Equal("verzia 3", File.ReadAllText(running));
        Assert.Equal("verzia 2", File.ReadAllText(running + LauncherSelfUpdater.SupersededSuffix));

        // Nothing else accumulates in the folder.
        Assert.Equal(2, Directory.GetFiles(temp.Path).Length);
    }
}
