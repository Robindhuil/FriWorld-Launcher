using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// An update is an offer, not a toll gate: a game already on disk stays playable, and the
/// minimum-launcher-version gate is the one thing allowed to stand in the way of installing.
/// </summary>
public class UpdateChoiceTests
{
    private const int SmallPayload = 128 * 1024;

    private static async Task<string> BuildRelease(string store, string version) =>
        await MockReleaseBuilder.BuildAsync(store, new MockReleaseBuilder.Options
        {
            Version = version,
            PayloadBytes = SmallPayload,
            Platforms = [PlatformKey.Current],
        });

    private static UpdateOrchestrator OrchestratorFor(string manifest, string root) =>
        LauncherConfiguration.Resolve(manifest, root).CreateOrchestrator();

    [Fact]
    public async Task With_nothing_installed_there_is_no_choice_to_offer()
    {
        using var temp = new TempDirectory("choice-fresh");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");

        var check = await OrchestratorFor(manifest, temp.Combine("root")).CheckAsync();

        Assert.True(check.UpdateRequired);
        Assert.False(check.CanPlayWithoutUpdating);
    }

    [Fact]
    public async Task With_a_newer_release_the_installed_one_stays_playable()
    {
        using var temp = new TempDirectory("choice-newer");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        await OrchestratorFor(await BuildRelease(store, "1.0.0-mock"), root).EnsureLatestAsync();

        var check = await OrchestratorFor(await BuildRelease(store, "1.1.0-mock"), root).CheckAsync();

        Assert.True(check.UpdateRequired);
        Assert.Equal(UpdateReason.VersionDiffers, check.Reason);

        // This is what turns the update into a question rather than a wall.
        Assert.True(check.CanPlayWithoutUpdating);
        Assert.Equal("1.0.0-mock", check.InstalledVersion);
    }

    [Fact]
    public async Task A_missing_install_is_not_playable_even_though_state_exists()
    {
        using var temp = new TempDirectory("choice-missing");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");
        var root = temp.Combine("root");

        var first = OrchestratorFor(manifest, root);
        await first.EnsureLatestAsync();
        Directory.Delete(first.Paths.Game, recursive: true);

        var check = await OrchestratorFor(manifest, root).CheckAsync();

        Assert.Equal(UpdateReason.InstallMissing, check.Reason);
        Assert.False(check.CanPlayWithoutUpdating);
    }

    [Fact]
    public async Task A_manifest_that_needs_a_newer_launcher_refuses_to_install()
    {
        using var temp = new TempDirectory("choice-gate");
        var store = temp.Combine("store");
        var manifestPath = await BuildRelease(store, "1.0.0-mock");

        // Raise the floor above whatever this launcher is.
        var manifest = ManifestJson.Parse(await File.ReadAllTextAsync(manifestPath));
        await File.WriteAllTextAsync(
            manifestPath,
            ManifestJson.Write(manifest with { MinLauncherVersion = "9999.0.0" }));

        var orchestrator = OrchestratorFor(manifestPath, temp.Combine("root"));
        var check = await orchestrator.CheckAsync();

        Assert.True(check.LauncherTooOld);

        // Checking still works — the launcher has to be able to say why it is stuck.
        var error = await Assert.ThrowsAsync<LauncherTooOldException>(
            () => orchestrator.InstallAsync(check));

        Assert.Contains("9999.0.0", error.Message, StringComparison.Ordinal);
        Assert.Null(orchestrator.State.Read());
    }

    [Fact]
    public async Task Without_a_floor_nothing_is_gated()
    {
        using var temp = new TempDirectory("choice-nogate");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");

        var check = await OrchestratorFor(manifest, temp.Combine("root")).CheckAsync();

        Assert.Null(check.Manifest.MinLauncherVersion);
        Assert.False(check.LauncherTooOld);
    }

    [Fact]
    public async Task Repair_reinstalls_over_a_damaged_installation()
    {
        using var temp = new TempDirectory("choice-repair");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");
        var root = temp.Combine("root");

        var orchestrator = OrchestratorFor(manifest, root);
        await orchestrator.EnsureLatestAsync();

        var installed = orchestrator.State.Read()!;
        var payload = Path.Combine(orchestrator.Paths.Game, "FriWorld_Data", "payload.bin");
        Assert.True(File.Exists(payload));

        // Something ate a file. The version check cannot notice, because it only compares tags.
        File.Delete(payload);
        Assert.Equal(UpdateReason.None, (await orchestrator.CheckAsync()).Reason);

        await orchestrator.RepairAsync();

        Assert.True(File.Exists(payload));
        Assert.Equal(installed.Version, orchestrator.State.Read()!.Version);
    }

    [Fact]
    public async Task Cancelling_a_download_leaves_the_previous_install_alone()
    {
        using var temp = new TempDirectory("choice-cancel");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        await OrchestratorFor(await BuildRelease(store, "1.0.0-mock"), root).EnsureLatestAsync();

        var orchestrator = OrchestratorFor(await BuildRelease(store, "1.1.0-mock"), root);
        var check = await orchestrator.CheckAsync();

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.InstallAsync(check, null, cancelled.Token));

        // The game that was there is still there, and still recorded.
        Assert.Equal("1.0.0-mock", orchestrator.State.Read()!.Version);
        Assert.True(File.Exists(Path.Combine(orchestrator.Paths.Game, "FriWorld_Data", "version.txt")));
    }
}
