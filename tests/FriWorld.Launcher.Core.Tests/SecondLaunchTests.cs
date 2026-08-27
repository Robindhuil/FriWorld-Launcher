using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Two copies of the game share one save directory and one set of settings files, and the second
/// to exit decides what the first one's session was worth. Starting a second one is refused.
/// </summary>
public class SecondLaunchTests
{
    private static async Task<UpdateOrchestrator> Installed(TempDirectory temp, int stubSeconds)
    {
        var manifest = await MockReleaseBuilder.BuildAsync(
            temp.Combine("store"),
            new MockReleaseBuilder.Options
            {
                Version = "1.0.0-mock",
                PayloadBytes = 64 * 1024,
                Platforms = [PlatformKey.Current],
                StubRunsForSeconds = stubSeconds,
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
        var orchestrator = await Installed(temp, stubSeconds: 5);

        var first = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));

        try
        {
            Assert.False(first.HasExited, "the stub was gone before the second launch was tried");

            await Assert.ThrowsAsync<GameIsRunningException>(
                () => orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1)));
        }
        finally
        {
            first.Kill(entireProcessTree: true);
            await first.WaitForExitAsync();
        }
    }

    [Fact]
    public async Task It_can_be_started_again_once_the_first_one_has_stopped()
    {
        // The guard has to be about what is running now, not about having ever been started.
        using var temp = new TempDirectory("second-launch-after");
        var orchestrator = await Installed(temp, stubSeconds: 0);

        var first = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));
        await first.WaitForExitAsync();

        // The process object is done, but the operating system takes a moment to stop listing it.
        await Task.Delay(300);

        var second = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));
        await second.WaitForExitAsync();
    }
}
