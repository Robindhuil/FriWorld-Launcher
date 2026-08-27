using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Two copies of the game share one save directory and one set of settings files, and whichever
/// exits last decides what the other's session was worth. Starting a second one is refused.
///
/// The refusal is driven through <see cref="Launch.GameLauncher.RunningExecutables"/> rather than
/// by starting a real second copy: a mock game is a shell script, so the process the operating
/// system runs is the interpreter and the real scan could never see it. A real Unity build is an
/// executable inside the install directory and is seen normally.
/// </summary>
public class SecondLaunchTests
{
    private static async Task<UpdateOrchestrator> Installed(TempDirectory temp)
    {
        var manifest = await MockReleaseBuilder.BuildAsync(
            temp.Combine("store"),
            new MockReleaseBuilder.Options
            {
                Version = "1.0.0-mock",
                PayloadBytes = 64 * 1024,
                Platforms = [PlatformKey.Current],
            });

        var orchestrator = LauncherConfiguration
            .Resolve(manifest, temp.Combine("root"))
            .CreateOrchestrator();

        await orchestrator.EnsureLatestAsync();
        return orchestrator;
    }

    [Fact]
    public async Task A_second_copy_is_refused_while_the_first_one_runs()
    {
        using var temp = new TempDirectory("second-launch");
        var orchestrator = await Installed(temp);

        var running = Path.Combine(orchestrator.Paths.Game, "FriWorld.exe");
        orchestrator.Launcher.RunningExecutables = () => [running];

        await Assert.ThrowsAsync<GameIsRunningException>(
            () => orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task Something_else_running_elsewhere_is_not_the_game()
    {
        // The rule is about this install, not about any process that happens to be around.
        using var temp = new TempDirectory("second-launch-elsewhere");
        var orchestrator = await Installed(temp);

        orchestrator.Launcher.RunningExecutables = () =>
        [
            Path.Combine(temp.Path, "somewhere-else", "FriWorld.exe"),
            "/usr/bin/whatever",
        ];

        var process = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));
        await process.WaitForExitAsync();
    }

    [Fact]
    public async Task It_can_be_started_again_once_the_first_one_has_stopped()
    {
        // The guard has to be about what is running now, not about having ever been started.
        using var temp = new TempDirectory("second-launch-after");
        var orchestrator = await Installed(temp);

        var running = Path.Combine(orchestrator.Paths.Game, "FriWorld.exe");
        var isRunning = true;
        orchestrator.Launcher.RunningExecutables = () => isRunning ? [running] : [];

        await Assert.ThrowsAsync<GameIsRunningException>(
            () => orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1)));

        isRunning = false;

        var process = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));
        await process.WaitForExitAsync();
    }

    [Fact]
    public async Task Updating_is_refused_for_the_same_reason()
    {
        // This rule predates the launch guard and must keep working: on Windows an open file
        // cannot be renamed away, so the swap would fail half done.
        using var temp = new TempDirectory("second-launch-update");
        var orchestrator = await Installed(temp);

        orchestrator.Launcher.RunningExecutables =
            () => [Path.Combine(orchestrator.Paths.Game, "FriWorld.exe")];

        var check = await orchestrator.CheckAsync();

        await Assert.ThrowsAsync<GameIsRunningException>(() => orchestrator.InstallAsync(check));
    }
}
