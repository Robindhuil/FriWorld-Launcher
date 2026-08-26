using FriWorld.Launcher.Core;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The launcher's minimum-version gate. This is the only place versions are ordered rather than
/// merely compared, and it is the escape hatch that makes the manifest format changeable later.
/// </summary>
public class VersionGateTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("2.0.0", "1.9.9", false)]
    public void Orders_release_versions(string current, string minimum, bool expectedOlder)
    {
        Assert.True(SemanticVersion.TryParse(current, out var a));
        Assert.True(SemanticVersion.TryParse(minimum, out var b));

        Assert.Equal(expectedOlder, a.CompareTo(b) < 0);
    }

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0-alpha", false)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", true)]
    [InlineData("1.0.0-beta", "1.0.0-alpha", false)]
    public void A_prerelease_precedes_its_release(string current, string minimum, bool expectedOlder)
    {
        Assert.True(SemanticVersion.TryParse(current, out var a));
        Assert.True(SemanticVersion.TryParse(minimum, out var b));

        Assert.Equal(expectedOlder, a.CompareTo(b) < 0);
    }

    [Fact]
    public void Build_metadata_does_not_affect_order()
    {
        Assert.True(SemanticVersion.TryParse("1.2.3+abc123", out var a));
        Assert.True(SemanticVersion.TryParse("1.2.3+def456", out var b));

        Assert.Equal(0, a.CompareTo(b));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2")]
    public void Missing_components_count_as_zero(string text)
    {
        Assert.True(SemanticVersion.TryParse(text, out var version));
        Assert.Equal(0, version.Patch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    public void Refuses_what_it_cannot_read(string? text) =>
        Assert.False(SemanticVersion.TryParse(text, out _));

    [Fact]
    public void No_minimum_never_locks_anyone_out()
    {
        // A gate that fires by accident would keep people out of a game that would have worked,
        // so anything unreadable is treated as no gate at all.
        Assert.False(LauncherVersion.IsOlderThan(null));
        Assert.False(LauncherVersion.IsOlderThan(""));
        Assert.False(LauncherVersion.IsOlderThan("nonsense"));
    }

    [Fact]
    public void The_running_launcher_is_never_older_than_itself() =>
        Assert.False(LauncherVersion.IsOlderThan(LauncherVersion.Current));

    [Fact]
    public void A_far_future_minimum_locks_the_gate() =>
        Assert.True(LauncherVersion.IsOlderThan("9999.0.0"));
}
