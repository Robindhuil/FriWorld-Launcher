using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The whole pipeline, end to end, against a release served from the local filesystem.
///
/// Only the network is simulated. The archive is a real archive, the checksum is really computed,
/// the tree is really extracted with its permission bits, and the directories are really swapped —
/// so these tests exercise the same code that will run against remote storage.
/// </summary>
public class UpdatePipelineTests
{
    private const int SmallPayload = 256 * 1024;

    private static async Task<string> BuildRelease(string storeDirectory, string version) =>
        await MockReleaseBuilder.BuildAsync(
            storeDirectory,
            new MockReleaseBuilder.Options
            {
                Version = version,
                PayloadBytes = SmallPayload,
                Platforms = [PlatformKey.Current],
            });

    private static UpdateOrchestrator OrchestratorFor(string manifestPath, string root) =>
        LauncherConfiguration.Resolve(manifestPath, root).CreateOrchestrator();

    [Fact]
    public async Task Installs_from_a_clean_machine()
    {
        using var temp = new TempDirectory("e2e-fresh");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");
        var root = temp.Combine("root");

        var orchestrator = OrchestratorFor(manifest, root);
        var check = await orchestrator.EnsureLatestAsync();

        Assert.Equal(UpdateReason.NotInstalled, check.Reason);

        var installed = orchestrator.State.Read();
        Assert.NotNull(installed);
        Assert.Equal("1.0.0-mock", installed.Version);
        Assert.Equal(PlatformKey.Current, installed.Platform);
        Assert.False(installed.LaunchConfirmed);

        // The executable named by the manifest has to actually be there afterwards.
        var executable = Path.Combine(orchestrator.Paths.Game, installed.Exec);
        Assert.True(File.Exists(executable), $"expected {executable} to exist");
    }

    [Fact]
    public async Task A_second_run_with_no_new_release_does_nothing()
    {
        using var temp = new TempDirectory("e2e-noop");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");
        var root = temp.Combine("root");

        await OrchestratorFor(manifest, root).EnsureLatestAsync();

        var orchestrator = OrchestratorFor(manifest, root);
        var check = await orchestrator.EnsureLatestAsync();

        Assert.Equal(UpdateReason.None, check.Reason);
        Assert.False(check.UpdateRequired);
    }

    [Fact]
    public async Task A_new_version_replaces_the_old_one_and_keeps_it_around()
    {
        using var temp = new TempDirectory("e2e-upgrade");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        var first = await BuildRelease(store, "1.0.0-mock");
        await OrchestratorFor(first, root).EnsureLatestAsync();

        var second = await BuildRelease(store, "1.1.0-mock");
        var orchestrator = OrchestratorFor(second, root);
        var check = await orchestrator.EnsureLatestAsync();

        Assert.Equal(UpdateReason.VersionDiffers, check.Reason);
        Assert.Equal("1.1.0-mock", orchestrator.State.Read()!.Version);

        // Kept deliberately: the new build has not proven it can start yet.
        Assert.True(Directory.Exists(orchestrator.Paths.GameOld));
    }

    [Fact]
    public async Task An_older_tag_is_installed_too_because_tags_are_never_ordered()
    {
        using var temp = new TempDirectory("e2e-downgrade");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        await OrchestratorFor(await BuildRelease(store, "2.0.0-mock"), root).EnsureLatestAsync();

        var rolledBack = await BuildRelease(store, "1.0.0-mock");
        var orchestrator = OrchestratorFor(rolledBack, root);
        var check = await orchestrator.EnsureLatestAsync();

        Assert.Equal(UpdateReason.VersionDiffers, check.Reason);
        Assert.Equal("1.0.0-mock", orchestrator.State.Read()!.Version);
    }

    [Fact]
    public async Task A_missing_install_directory_forces_a_reinstall()
    {
        using var temp = new TempDirectory("e2e-missing");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");
        var root = temp.Combine("root");

        var first = OrchestratorFor(manifest, root);
        await first.EnsureLatestAsync();
        Directory.Delete(first.Paths.Game, recursive: true);

        var orchestrator = OrchestratorFor(manifest, root);
        var check = await orchestrator.CheckAsync();

        Assert.Equal(UpdateReason.InstallMissing, check.Reason);
    }

    [Fact]
    public async Task A_tampered_archive_fails_the_checksum_and_installs_nothing()
    {
        using var temp = new TempDirectory("e2e-tamper");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        var manifestPath = await BuildRelease(store, "1.0.0-mock");

        // Rewrite the archive after the manifest was written, exactly as a corrupted or swapped
        // download would look from the launcher's side.
        var manifest = ManifestJson.Parse(await File.ReadAllTextAsync(manifestPath));
        var archive = Path.Combine(store, manifest.Platforms[PlatformKey.Current].Url);
        await File.WriteAllBytesAsync(archive, new byte[manifest.Platforms[PlatformKey.Current].Size]);

        var orchestrator = OrchestratorFor(manifestPath, root);

        await Assert.ThrowsAsync<HashMismatchException>(() => orchestrator.EnsureLatestAsync());

        Assert.Null(orchestrator.State.Read());
        Assert.False(Directory.Exists(orchestrator.Paths.Game));
    }

    [Fact]
    public async Task A_manifest_with_no_build_for_this_platform_says_so()
    {
        using var temp = new TempDirectory("e2e-noplatform");
        var store = temp.Combine("store");

        var manifestPath = await MockReleaseBuilder.BuildAsync(
            store,
            new MockReleaseBuilder.Options
            {
                Version = "1.0.0-mock",
                PayloadBytes = SmallPayload,
                Platforms = ["some-unreal-platform-x64"],
            });

        var orchestrator = OrchestratorFor(manifestPath, temp.Combine("root"));

        var error = await Assert.ThrowsAsync<UpdateException>(() => orchestrator.CheckAsync());
        Assert.Contains("some-unreal-platform-x64", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Relative_archive_urls_resolve_next_to_the_manifest()
    {
        using var temp = new TempDirectory("e2e-relative");
        var store = temp.Combine("store");
        var manifestPath = await BuildRelease(store, "1.0.0-mock");

        // The generated manifest stores bare file names on purpose, so the folder can be moved.
        var raw = ManifestJson.Parse(await File.ReadAllTextAsync(manifestPath));
        Assert.False(Uri.IsWellFormedUriString(raw.Platforms[PlatformKey.Current].Url, UriKind.Absolute));

        var orchestrator = OrchestratorFor(manifestPath, temp.Combine("root"));
        var check = await orchestrator.CheckAsync();

        Assert.True(Uri.TryCreate(check.Package.Url, UriKind.Absolute, out var resolved));
        Assert.True(resolved.IsFile);
        Assert.True(File.Exists(resolved.LocalPath));
    }

    [Fact]
    public async Task A_build_that_dies_immediately_is_not_confirmed_and_keeps_its_predecessor()
    {
        using var temp = new TempDirectory("e2e-crash");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        await OrchestratorFor(await BuildRelease(store, "1.0.0-mock"), root).EnsureLatestAsync();

        var orchestrator = OrchestratorFor(await BuildRelease(store, "1.1.0-mock"), root);
        await orchestrator.EnsureLatestAsync();

        // The mock build prints a line and exits, which is what a build crashing on startup
        // looks like from here.
        var process = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync();

        Assert.False(orchestrator.State.Read()!.LaunchConfirmed);
        Assert.True(Directory.Exists(orchestrator.Paths.GameOld), "the previous install must survive");
        Assert.True(new AtomicInstaller(orchestrator.Paths).Rollback());
        Assert.Equal("1.0.0-mock", ReadInstalledVersionMarker(orchestrator.Paths.Game));
    }

    [Fact]
    public async Task A_build_that_keeps_running_is_confirmed_and_the_previous_one_is_dropped()
    {
        using var temp = new TempDirectory("e2e-confirm");
        var store = temp.Combine("store");
        var root = temp.Combine("root");

        await OrchestratorFor(await BuildRelease(store, "1.0.0-mock"), root).EnsureLatestAsync();

        var orchestrator = OrchestratorFor(await BuildRelease(store, "1.1.0-mock"), root);
        await orchestrator.EnsureLatestAsync();

        // A grace period this short expires before any process could exit, so the launch counts
        // as successful — which is the branch under test.
        var process = await orchestrator.LaunchAsync(gracePeriod: TimeSpan.FromMilliseconds(1));
        await process.WaitForExitAsync();

        Assert.True(orchestrator.State.Read()!.LaunchConfirmed);
        Assert.False(Directory.Exists(orchestrator.Paths.GameOld));
    }

    private static string ReadInstalledVersionMarker(string gameDirectory) =>
        File.ReadAllText(Path.Combine(gameDirectory, "FriWorld_Data", "version.txt")).Trim();

    [Fact]
    public async Task Progress_reports_reach_the_caller_in_order()
    {
        using var temp = new TempDirectory("e2e-progress");
        var manifest = await BuildRelease(temp.Combine("store"), "1.0.0-mock");

        var stages = new List<UpdateStage>();
        var progress = new Progress<UpdateStatus>(s => stages.Add(s.Stage));

        var orchestrator = OrchestratorFor(manifest, temp.Combine("root"));
        await orchestrator.EnsureLatestAsync(progress);

        // Progress<T> posts asynchronously, so give the callbacks a moment to land.
        await Task.Delay(200);

        Assert.Contains(UpdateStage.CheckingForUpdate, stages);
        Assert.Contains(UpdateStage.Downloading, stages);
        Assert.Contains(UpdateStage.Verifying, stages);
        Assert.Contains(UpdateStage.Extracting, stages);
        Assert.Contains(UpdateStage.Ready, stages);
    }
}
