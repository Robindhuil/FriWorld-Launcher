using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Removing the game. These tests care less about what goes than about what stays: uninstalling
/// is irreversible, so the things it must not touch matter more than the things it must.
/// </summary>
public class UninstallTests
{
    private static async Task<UpdateOrchestrator> Installed(TempDirectory temp, string version = "1.0.0-mock")
    {
        var manifest = await MockReleaseBuilder.BuildAsync(
            temp.Combine("store"),
            new MockReleaseBuilder.Options
            {
                Version = version,
                PayloadBytes = 128 * 1024,
                Platforms = [PlatformKey.Current],
            });

        var orchestrator = LauncherConfiguration
            .Resolve(manifest, temp.Combine("root"))
            .CreateOrchestrator();

        await orchestrator.EnsureLatestAsync();
        return orchestrator;
    }

    [Fact]
    public async Task Removes_the_game_and_forgets_it_was_installed()
    {
        using var temp = new TempDirectory("uninstall-basic");
        var orchestrator = await Installed(temp);

        Assert.True(Directory.Exists(orchestrator.Paths.Game));

        orchestrator.Uninstall();

        Assert.False(Directory.Exists(orchestrator.Paths.Game));
        Assert.Null(orchestrator.State.Read());
    }

    [Fact]
    public async Task Removes_the_scratch_and_previous_installs_too()
    {
        using var temp = new TempDirectory("uninstall-all");
        var orchestrator = await Installed(temp, "1.0.0-mock");

        // A second install leaves game.old behind until the new build has started once.
        var second = LauncherConfiguration
            .Resolve(
                await MockReleaseBuilder.BuildAsync(temp.Combine("store"), new MockReleaseBuilder.Options
                {
                    Version = "1.1.0-mock",
                    PayloadBytes = 128 * 1024,
                    Platforms = [PlatformKey.Current],
                }),
                temp.Combine("root"))
            .CreateOrchestrator();

        await second.EnsureLatestAsync();
        Assert.True(Directory.Exists(second.Paths.GameOld));

        second.Uninstall();

        Assert.False(Directory.Exists(second.Paths.Game));
        Assert.False(Directory.Exists(second.Paths.GameOld));
        Assert.False(Directory.Exists(second.Paths.GameNew));
        Assert.False(Directory.Exists(second.Paths.Cache));
    }

    [Fact]
    public async Task Keeps_the_log()
    {
        // The most likely reason someone uninstalls is that something went wrong, and the log
        // is the only record of what. Removing it with the game would throw away the evidence.
        using var temp = new TempDirectory("uninstall-log");
        var orchestrator = await Installed(temp);

        Assert.True(File.Exists(orchestrator.Paths.LogFile));

        orchestrator.Uninstall();

        Assert.True(File.Exists(orchestrator.Paths.LogFile));
    }

    [Fact]
    public async Task Leaves_the_install_root_itself_alone()
    {
        using var temp = new TempDirectory("uninstall-root");
        var orchestrator = await Installed(temp);

        orchestrator.Uninstall();

        Assert.True(Directory.Exists(orchestrator.Paths.Root));
    }

    [Fact]
    public void Uninstalling_nothing_is_not_an_error()
    {
        using var temp = new TempDirectory("uninstall-empty");
        var orchestrator = LauncherConfiguration
            .Resolve("https://friworld.example/manifest.json", temp.Combine("root"))
            .CreateOrchestrator();

        orchestrator.Uninstall();

        Assert.Null(orchestrator.State.Read());
    }

    [Fact]
    public async Task Installing_again_after_uninstalling_works()
    {
        using var temp = new TempDirectory("uninstall-reinstall");
        var manifest = await MockReleaseBuilder.BuildAsync(
            temp.Combine("store"),
            new MockReleaseBuilder.Options
            {
                Version = "1.0.0-mock",
                PayloadBytes = 128 * 1024,
                Platforms = [PlatformKey.Current],
            });

        var orchestrator = LauncherConfiguration.Resolve(manifest, temp.Combine("root")).CreateOrchestrator();
        await orchestrator.EnsureLatestAsync();
        orchestrator.Uninstall();

        var check = await orchestrator.CheckAsync();
        Assert.Equal(UpdateReason.NotInstalled, check.Reason);
        Assert.Equal(LauncherAction.Install, LauncherActions.AfterCheck(check));

        await orchestrator.EnsureLatestAsync();
        Assert.Equal("1.0.0-mock", orchestrator.State.Read()!.Version);
    }

    [Fact]
    public async Task The_installed_executable_can_be_located_for_the_file_manager()
    {
        using var temp = new TempDirectory("uninstall-reveal");
        var orchestrator = await Installed(temp);

        var path = orchestrator.InstalledExecutablePath();

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.StartsWith(orchestrator.Paths.Game, path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nothing_to_reveal_once_the_game_is_gone()
    {
        using var temp = new TempDirectory("uninstall-noreveal");
        var orchestrator = await Installed(temp);

        orchestrator.Uninstall();

        Assert.Null(orchestrator.InstalledExecutablePath());
    }

    [Fact]
    public void Nothing_to_reveal_when_nothing_is_installed()
    {
        using var temp = new TempDirectory("reveal-empty");
        var orchestrator = LauncherConfiguration
            .Resolve("https://friworld.example/manifest.json", temp.Combine("root"))
            .CreateOrchestrator();

        Assert.Null(orchestrator.InstalledExecutablePath());
    }
}
