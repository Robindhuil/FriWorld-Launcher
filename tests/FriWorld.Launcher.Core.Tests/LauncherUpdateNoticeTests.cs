using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The launcher notices a newer version of itself and says so. It never acts on it —
/// replacing a running executable is out of scope on purpose.
/// </summary>
public class LauncherUpdateNoticeTests
{
    private static UpdateCheck CheckWith(LauncherRelease? launcher) => new()
    {
        Manifest = new ReleaseManifest
        {
            Version = "1.0.0",
            Platforms = new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase),
            Launcher = launcher,
        },
        PlatformKey = PlatformKey.Current,
        Package = new PlatformPackage(),
        Installed = null,
        Reason = UpdateReason.None,
    };

    [Fact]
    public void No_launcher_section_means_no_notice() =>
        Assert.Null(CheckWith(null).LauncherUpdate);

    [Fact]
    public void The_running_version_is_not_offered_as_an_update()
    {
        var check = CheckWith(new LauncherRelease
        {
            Version = LauncherVersion.Current,
            DownloadUrl = "https://friworld.example/download",
        });

        Assert.Null(check.LauncherUpdate);
    }

    [Fact]
    public void A_different_version_is_offered()
    {
        var check = CheckWith(new LauncherRelease
        {
            Version = LauncherVersion.Current + "-next",
            DownloadUrl = "https://friworld.example/download",
            Notes = "Faster downloads.",
        });

        Assert.NotNull(check.LauncherUpdate);
        Assert.Equal("Faster downloads.", check.LauncherUpdate.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ms-settings:windowsdefender")]
    public void A_download_url_that_is_not_a_web_page_is_ignored(string url)
    {
        // The manifest arrives over the network. A non-web scheme handed to the shell could
        // start a local program, so those entries are dropped rather than shown.
        var check = CheckWith(new LauncherRelease
        {
            Version = LauncherVersion.Current + "-next",
            DownloadUrl = url,
        });

        Assert.Null(check.LauncherUpdate);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ms-settings:windowsdefender")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void The_browser_helper_refuses_anything_that_is_not_http(string url) =>
        Assert.False(SystemBrowser.TryOpen(url));

    [Fact]
    public void The_launcher_reports_its_own_version()
    {
        Assert.False(string.IsNullOrWhiteSpace(LauncherVersion.Current));
        Assert.False(LauncherVersion.DiffersFrom(LauncherVersion.Current));
        Assert.False(LauncherVersion.DiffersFrom(null));
        Assert.True(LauncherVersion.DiffersFrom("999.0.0"));
    }

    [Fact]
    public void The_launcher_version_carries_no_commit_suffix() =>
        Assert.DoesNotContain('+', LauncherVersion.Current);
}
