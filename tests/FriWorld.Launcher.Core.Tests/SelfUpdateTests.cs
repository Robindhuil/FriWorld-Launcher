using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Net;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The launcher replacing itself. This is the code most able to leave someone with nothing that
/// runs, so the tests care less about the happy path than about what survives a failure.
///
/// The expected texts are Slovak because these are read by players. Developer-facing output,
/// such as the CLI's help, stays English — the split is by audience, not by project.
/// </summary>
public class SelfUpdateTests
{
    private static LauncherSelfUpdater Updater() =>
        new(CompositeContentClient.CreateDefault());

    [Fact]
    public void A_binary_is_only_usable_over_https()
    {
        // The launcher replaces itself with this file. A manifest fetched over a hijacked
        // connection must not be able to hand over an executable.
        var sha = new string('a', 64);

        Assert.False(new LauncherBinary { Url = "http://a.test/l.exe", Sha256 = sha, Size = 1 }.IsUsable);
        Assert.False(new LauncherBinary { Url = "file:///C:/l.exe", Sha256 = sha, Size = 1 }.IsUsable);
        Assert.True(new LauncherBinary { Url = "https://a.test/l.exe", Sha256 = sha, Size = 1 }.IsUsable);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("tooshort", 1)]
    public void A_binary_without_a_full_checksum_is_refused(string sha, long size) =>
        Assert.False(new LauncherBinary { Url = "https://a.test/l.exe", Sha256 = sha, Size = size }.IsUsable);

    [Fact]
    public void A_binary_with_no_size_is_refused() =>
        Assert.False(new LauncherBinary
        {
            Url = "https://a.test/l.exe",
            Sha256 = new string('a', 64),
            Size = 0,
        }.IsUsable);

    [Fact]
    public void A_release_with_no_binary_for_this_platform_offers_only_the_link()
    {
        var release = new LauncherRelease
        {
            Version = "9.9.9",
            DownloadUrl = "https://friworld.example/download",
            Platforms = new Dictionary<string, LauncherBinary>(StringComparer.OrdinalIgnoreCase)
            {
                ["some-other-platform"] = new()
                {
                    Url = "https://a.test/l",
                    Sha256 = new string('a', 64),
                    Size = 10,
                },
            },
        };

        Assert.True(release.IsUsable);
        Assert.Null(release.BinaryForThisPlatform);
    }

    [Fact]
    public void A_release_carrying_this_platform_offers_the_binary()
    {
        var release = new LauncherRelease
        {
            Version = "9.9.9",
            DownloadUrl = "https://friworld.example/download",
            Platforms = new Dictionary<string, LauncherBinary>(StringComparer.OrdinalIgnoreCase)
            {
                [PlatformKey.Current] = new()
                {
                    Url = "https://a.test/l",
                    Sha256 = new string('a', 64),
                    Size = 10,
                },
            },
        };

        Assert.NotNull(release.BinaryForThisPlatform);
    }

    [Fact]
    public async Task A_binary_that_fails_its_checksum_is_never_staged()
    {
        using var temp = new TempDirectory("self-badhash");
        var source = temp.Combine("new-launcher.bin");
        await File.WriteAllTextAsync(source, "pretend this is a launcher");

        // Staging goes through the same verifier the game archive does, so the guarantee is the
        // same one: nothing unverified is ever put where it could be executed.
        await Assert.ThrowsAsync<HashMismatchException>(
            () => Sha256Verifier.VerifyOrDeleteAsync(source, new string('b', 64)));

        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task An_unusable_binary_is_rejected_before_anything_is_downloaded()
    {
        var error = await Assert.ThrowsAsync<LauncherUpdateException>(
            () => Updater().StageAsync(new LauncherBinary { Url = "http://a.test/l.exe" }));

        Assert.Contains("https", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_test_host_is_correctly_reported_as_unable_to_self_update()
    {
        // Tests run from a multi-file build, which is exactly the deployment that must refuse.
        // A half-replaced launcher is worse than an old one.
        Assert.False(Updater().IsSelfContainedSingleFile);
        Assert.NotNull(Updater().BlockedReason());
    }

    [Fact]
    public void Applying_an_update_is_refused_when_the_deployment_cannot_take_one()
    {
        using var temp = new TempDirectory("self-blocked");
        var staged = temp.Combine("staged.exe");
        File.WriteAllText(staged, "new");

        Assert.Throws<LauncherUpdateException>(() => Updater().Apply(staged));

        // Refusing must not consume the staged file either.
        Assert.True(File.Exists(staged));
    }

    [Fact]
    public void Discarding_a_staged_file_is_safe_to_call_twice()
    {
        using var temp = new TempDirectory("self-discard");
        var staged = temp.Combine("staged.exe");
        File.WriteAllText(staged, "new");

        LauncherSelfUpdater.DiscardStaged(staged);
        LauncherSelfUpdater.DiscardStaged(staged);

        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void Cleaning_up_a_missing_superseded_file_does_nothing_bad() =>
        Updater().CleanUpSupersededExecutable();

    [Theory]
    [InlineData(typeof(GameIsRunningException), "Hra už beží.")]
    [InlineData(typeof(LauncherTooOldException), "Tento launcher je príliš starý.")]
    public void Known_failures_are_described_in_words_a_player_can_act_on(Type type, string headline)
    {
        var exception = (Exception)Activator.CreateInstance(type, "raw technical text")!;

        Assert.Equal(headline, FailureMessages.Describe(exception).Headline);
    }

    [Fact]
    public void A_hash_mismatch_is_described_as_recoverable()
    {
        var message = FailureMessages.Describe(new HashMismatchException("sha mismatch"));

        Assert.True(message.Recoverable);
        Assert.Contains("poškoden", message.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Running_out_of_space_explains_why_it_needs_so_much()
    {
        var message = FailureMessages.Describe(new InsufficientDiskSpaceException("need 3 GB"));

        Assert.False(string.IsNullOrWhiteSpace(message.Advice));
        Assert.True(message.Recoverable);
    }

    [Fact]
    public void A_launcher_too_old_failure_is_not_worth_retrying() =>
        Assert.False(FailureMessages.Describe(new LauncherTooOldException("x")).Recoverable);

    [Fact]
    public void An_unknown_failure_still_says_something()
    {
        var message = FailureMessages.Describe(new InvalidOperationException("internal detail"));

        Assert.Equal("Niečo sa pokazilo.", message.Headline);
        Assert.Equal("internal detail", message.Advice);
    }
}
