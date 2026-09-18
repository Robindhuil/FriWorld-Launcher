using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Launch;
using System.Net.Http;
using FriWorld.Launcher.Core.Localization;
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

    public static TheoryData<Exception, string, string> KnownFailures => new()
    {
        { new GameIsRunningException("raw technical text"), "Hra už beží.", "The game is already running." },
        { new LauncherTooOldException("raw technical text"), "Tento launcher je príliš starý.", "This launcher is too old." },
        { new HttpRequestException("raw technical text"), "Nepodarilo sa spojiť so serverom.", "The server could not be reached." },
    };

    [Theory]
    [MemberData(nameof(KnownFailures))]
    public void Known_failures_are_described_in_words_a_player_can_act_on(
        Exception exception, string slovak, string english)
    {
        Assert.Equal(slovak, FailureMessages.Describe(exception, Texts.Slovak).Headline);
        Assert.Equal(english, FailureMessages.Describe(exception, Texts.English).Headline);
    }

    [Fact]
    public void A_hash_mismatch_is_described_as_recoverable()
    {
        var message = FailureMessages.Describe(new HashMismatchException("sha mismatch"), Texts.Slovak);

        Assert.True(message.Recoverable);
        Assert.Contains("poškoden", message.Headline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Running_out_of_space_explains_why_it_needs_so_much()
    {
        var message = FailureMessages.Describe(new InsufficientDiskSpaceException("need 3 GB"), Texts.Slovak);

        Assert.False(string.IsNullOrWhiteSpace(message.Advice));
        Assert.True(message.Recoverable);
    }

    [Fact]
    public void Running_out_of_space_puts_the_real_numbers_in_the_player_s_language()
    {
        var exception = new InsufficientDiskSpaceException(
            "Need about 1.5 GB free on C:\\ ...", 1_610_612_736, 536_870_912, "C:\\");

        Assert.Contains("1,5 GB", FailureMessages.Describe(exception, Texts.Slovak).Advice);
        Assert.Contains("1.5 GB", FailureMessages.Describe(exception, Texts.English).Advice);
    }

    [Fact]
    public void A_launcher_too_old_failure_is_not_worth_retrying() =>
        Assert.False(FailureMessages.Describe(new LauncherTooOldException("x"), Texts.Slovak).Recoverable);

    [Fact]
    public void A_launcher_too_old_failure_names_both_versions()
    {
        var exception = new LauncherTooOldException("raw", "0.3.0-alpha", "0.1.8-alpha");
        var advice = FailureMessages.Describe(exception, Texts.English).Advice;

        Assert.Contains("0.3.0-alpha", advice);
        Assert.Contains("0.1.8-alpha", advice);
    }

    [Fact]
    public void An_unknown_failure_still_says_something()
    {
        var message = FailureMessages.Describe(new InvalidOperationException("internal detail"), Texts.Slovak);

        Assert.Equal("Niečo sa pokazilo.", message.Headline);
        Assert.False(string.IsNullOrWhiteSpace(message.Advice));
    }

    [Fact]
    public void A_failure_never_shows_a_player_the_exception_s_own_english()
    {
        // The bug this exists for: the advice used to be a Slovak sentence with the exception's
        // English message pasted in front of it, so a player read half a translation.
        Exception[] failures =
        [
            new InvalidOperationException("internal detail"),
            new UpdateException("Release 1.0 has no build for win-x64."),
            new GameLaunchException(GameLaunchProblem.ExecutableNotNamed, "The manifest does not say which file to run."),
            new LauncherUpdateException(LauncherUpdateProblem.Other, "Installing the new launcher failed: access denied."),
            new IOException("The process cannot access the file."),
        ];

        foreach (var failure in failures)
        {
            var message = FailureMessages.Describe(failure, Texts.Slovak);

            Assert.DoesNotContain(failure.Message, message.Headline);
            Assert.DoesNotContain(failure.Message, message.Advice ?? string.Empty);
        }
    }
}
