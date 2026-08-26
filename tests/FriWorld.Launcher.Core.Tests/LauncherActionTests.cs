using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// What the launcher offers to do next.
///
/// The rule these all serve: nothing large happens without someone asking for it. A check never
/// turns into a download on its own, however obvious the download looks.
/// </summary>
public class LauncherActionTests
{
    private static UpdateCheck Check(
        UpdateReason reason,
        InstalledState? installed,
        string? minLauncher = null) => new()
    {
        Manifest = new ReleaseManifest
        {
            Version = "2.0.0",
            MinLauncherVersion = minLauncher,
            Platforms = new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase)
            {
                [PlatformKey.Current] = new()
                {
                    Url = "https://a.test/game.zip",
                    Sha256 = new string('a', 64),
                    Size = 100,
                    Exec = "FriWorld.exe",
                },
            },
        },
        PlatformKey = PlatformKey.Current,
        Package = new PlatformPackage(),
        Installed = installed,
        Reason = reason,
    };

    private static InstalledState Installed(string version = "1.0.0") => new()
    {
        Version = version,
        Platform = PlatformKey.Current,
        InstalledAt = DateTimeOffset.UtcNow,
        Sha256 = new string('a', 64),
        Exec = "FriWorld.exe",
    };

    [Fact]
    public void Nothing_installed_offers_install_and_never_starts_one() =>
        Assert.Equal(
            LauncherAction.Install,
            LauncherActions.AfterCheck(Check(UpdateReason.NotInstalled, null)));

    [Fact]
    public void An_install_that_vanished_is_offered_again_rather_than_played() =>
        Assert.Equal(
            LauncherAction.Install,
            LauncherActions.AfterCheck(Check(UpdateReason.InstallMissing, Installed())));

    [Fact]
    public void A_build_for_another_platform_counts_as_not_installed() =>
        Assert.Equal(
            LauncherAction.Install,
            LauncherActions.AfterCheck(Check(UpdateReason.PlatformDiffers, Installed())));

    [Fact]
    public void Up_to_date_offers_play() =>
        Assert.Equal(
            LauncherAction.Play,
            LauncherActions.AfterCheck(Check(UpdateReason.None, Installed())));

    [Fact]
    public void A_newer_release_offers_update_while_the_old_one_stays_playable()
    {
        var check = Check(UpdateReason.VersionDiffers, Installed());

        Assert.Equal(LauncherAction.Update, LauncherActions.AfterCheck(check));

        // The secondary offer rests on this staying true.
        Assert.True(check.CanPlayWithoutUpdating);
    }

    [Fact]
    public void A_launcher_too_old_can_still_play_what_is_installed() =>
        Assert.Equal(
            LauncherAction.Play,
            LauncherActions.AfterCheck(Check(UpdateReason.VersionDiffers, Installed(), minLauncher: "9999.0.0")));

    [Fact]
    public void A_launcher_too_old_with_nothing_installed_can_offer_nothing() =>
        Assert.Equal(
            LauncherAction.None,
            LauncherActions.AfterCheck(Check(UpdateReason.NotInstalled, null, minLauncher: "9999.0.0")));

    [Fact]
    public void After_a_failed_first_check_the_only_move_is_to_try_again() =>
        Assert.Equal(
            LauncherAction.Retry,
            LauncherActions.AfterInterruption(null, anythingInstalled: false));

    [Fact]
    public void After_a_cancelled_first_install_the_offer_stands() =>
        Assert.Equal(
            LauncherAction.Install,
            LauncherActions.AfterInterruption(Check(UpdateReason.NotInstalled, null), anythingInstalled: false));

    [Fact]
    public void After_a_cancelled_update_the_installed_build_is_still_playable() =>
        Assert.Equal(
            LauncherAction.Update,
            LauncherActions.AfterInterruption(Check(UpdateReason.VersionDiffers, Installed()), anythingInstalled: true));

    [Fact]
    public void A_failure_with_nothing_pending_leaves_play_on_the_button() =>
        Assert.Equal(
            LauncherAction.Play,
            LauncherActions.AfterInterruption(Check(UpdateReason.None, Installed()), anythingInstalled: true));

    [Fact]
    public void An_install_on_disk_outranks_a_check_that_never_finished() =>
        Assert.Equal(
            LauncherAction.Play,
            LauncherActions.AfterInterruption(null, anythingInstalled: true));
}
