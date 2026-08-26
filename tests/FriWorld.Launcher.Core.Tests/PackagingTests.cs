using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Extract;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Packaging;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Packing a Unity build into a release, and then consuming that release with the launcher.
/// Because both halves live here, a release this code produces is one the launcher can read
/// by construction rather than by agreement.
/// </summary>
public class PackagingTests
{
    /// <summary>Writes something shaped like Unity's player output.</summary>
    private static void FakePlayerOutput(string directory, string platformKey)
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "FriWorld_Data"));
        File.WriteAllText(Path.Combine(directory, "FriWorld_Data", "level0"), "scene bytes");
        File.WriteAllText(Path.Combine(directory, "UnityPlayer.dll"), "engine");

        switch (platformKey)
        {
            case PlatformKey.WindowsX64:
                File.WriteAllText(Path.Combine(directory, "FriWorld.exe"), "windows player");
                // Unity ships this next to the game; it must not be mistaken for the game.
                File.WriteAllText(Path.Combine(directory, "UnityCrashHandler64.exe"), "crash handler");
                break;

            case PlatformKey.LinuxX64:
                File.WriteAllText(Path.Combine(directory, "FriWorld.x86_64"), "linux player");
                break;

            default:
                var macOs = Path.Combine(directory, "FriWorld.app", "Contents", "MacOS");
                Directory.CreateDirectory(macOs);
                File.WriteAllText(Path.Combine(macOs, "FriWorld"), "mac player");
                break;
        }
    }

    [Theory]
    [InlineData("FriWorld_BurstDebugInformation_DoNotShip")]
    [InlineData("FriWorld_BackUpThisFolder_ButDontShipItWithYourGame")]
    public async Task Folders_unity_marks_as_not_for_shipping_stay_out_of_the_archive(string folder)
    {
        using var temp = new TempDirectory("pack-donotship");
        var input = temp.Combine("Build");
        var player = Path.Combine(input, PlatformKey.WindowsX64);
        FakePlayerOutput(player, PlatformKey.WindowsX64);

        // Unity emits these beside the player and says so in the folder name, but it does not
        // remove them. They hold debug symbols and absolute paths from the build machine.
        var nested = Path.Combine(player, folder, "Data", "Plugins");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "symbols.txt"), "C:/build/agent/secret/path");

        Assert.Contains(folder, ArchiveBuilder.ExcludedEntries(player));

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
        });

        var extracted = temp.Combine("extracted");
        await ArchiveExtractors.For(ArchiveFormat.Zip)
            .ExtractAsync(result.Platforms.Single().ArchivePath, extracted, null, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(extracted, folder)));
        Assert.True(File.Exists(Path.Combine(extracted, "FriWorld.exe")));
    }

    [Fact]
    public async Task The_same_exclusion_applies_to_tar_archives()
    {
        using var temp = new TempDirectory("pack-donotship-tar");
        var input = temp.Combine("Build");
        var player = Path.Combine(input, PlatformKey.LinuxX64);
        FakePlayerOutput(player, PlatformKey.LinuxX64);

        Directory.CreateDirectory(Path.Combine(player, "FriWorld_BurstDebugInformation_DoNotShip"));
        await File.WriteAllTextAsync(
            Path.Combine(player, "FriWorld_BurstDebugInformation_DoNotShip", "symbols.txt"), "x");

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
        });

        var names = (await ReadTarModes(result.Platforms.Single().ArchivePath)).Keys;

        Assert.DoesNotContain(names, n => n.Contains("DoNotShip", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("FriWorld.x86_64", names);
    }

    [Fact]
    public void Finds_the_windows_executable_and_ignores_the_crash_handler()
    {
        using var temp = new TempDirectory("find-win");
        FakePlayerOutput(temp.Path, PlatformKey.WindowsX64);

        Assert.Equal("FriWorld.exe", ExecutableFinder.Find(temp.Path, PlatformKey.WindowsX64));
    }

    [Fact]
    public void Finds_the_linux_executable_under_its_unity_name()
    {
        using var temp = new TempDirectory("find-linux");
        FakePlayerOutput(temp.Path, PlatformKey.LinuxX64);

        // Unity names it <product>.x86_64, which is the detail most likely to be typed wrong.
        Assert.Equal("FriWorld.x86_64", ExecutableFinder.Find(temp.Path, PlatformKey.LinuxX64));
    }

    [Fact]
    public void Finds_the_binary_inside_a_mac_bundle_rather_than_the_bundle()
    {
        using var temp = new TempDirectory("find-mac");
        FakePlayerOutput(temp.Path, PlatformKey.MacArm64);

        Assert.Equal(
            "FriWorld.app/Contents/MacOS/FriWorld",
            ExecutableFinder.Find(temp.Path, PlatformKey.MacArm64));
    }

    [Fact]
    public void Says_so_when_it_cannot_tell()
    {
        using var temp = new TempDirectory("find-nothing");
        Directory.CreateDirectory(temp.Path);

        var error = Assert.Throws<PackagingException>(
            () => ExecutableFinder.Find(temp.Path, PlatformKey.WindowsX64));

        Assert.Contains("--exec", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Packs_every_platform_subfolder_it_finds()
    {
        using var temp = new TempDirectory("pack-all");
        var input = temp.Combine("Build");

        FakePlayerOutput(Path.Combine(input, PlatformKey.WindowsX64), PlatformKey.WindowsX64);
        FakePlayerOutput(Path.Combine(input, PlatformKey.LinuxX64), PlatformKey.LinuxX64);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "0.1.2-alpha",
            Notes = "Test release.",
        });

        Assert.Equal(2, result.Platforms.Count);
        Assert.True(File.Exists(result.ManifestPath));

        var manifest = ManifestJson.Parse(await File.ReadAllTextAsync(result.ManifestPath));

        Assert.Equal("0.1.2-alpha", manifest.Version);
        Assert.Equal(ArchiveFormat.Zip, manifest.Platforms[PlatformKey.WindowsX64].ResolvedFormat);
        Assert.Equal(ArchiveFormat.TarGz, manifest.Platforms[PlatformKey.LinuxX64].ResolvedFormat);
        Assert.Equal("FriWorld.exe", manifest.Platforms[PlatformKey.WindowsX64].Exec);
        Assert.Equal("FriWorld.x86_64", manifest.Platforms[PlatformKey.LinuxX64].Exec);
    }

    [Fact]
    public async Task An_explicit_exec_overrides_detection()
    {
        using var temp = new TempDirectory("pack-override");
        var input = temp.Combine("Build");
        FakePlayerOutput(Path.Combine(input, PlatformKey.WindowsX64), PlatformKey.WindowsX64);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
            ExecOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlatformKey.WindowsX64] = "Sub/Other.exe",
            },
        });

        var manifest = ManifestJson.Parse(await File.ReadAllTextAsync(result.ManifestPath));
        Assert.Equal("Sub/Other.exe", manifest.Platforms[PlatformKey.WindowsX64].Exec);
    }

    [Fact]
    public async Task A_base_url_produces_absolute_archive_urls()
    {
        using var temp = new TempDirectory("pack-baseurl");
        var input = temp.Combine("Build");
        FakePlayerOutput(Path.Combine(input, PlatformKey.WindowsX64), PlatformKey.WindowsX64);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
            BaseUrl = "https://friworld.example/releases/1.0.0",
        });

        var manifest = ManifestJson.Parse(await File.ReadAllTextAsync(result.ManifestPath));

        Assert.Equal(
            "https://friworld.example/releases/1.0.0/FriWorld-1.0.0-win-x64.zip",
            manifest.Platforms[PlatformKey.WindowsX64].Url);
    }

    [Fact]
    public async Task Refuses_an_input_folder_with_no_platforms()
    {
        using var temp = new TempDirectory("pack-empty");
        var input = temp.Combine("Build");
        Directory.CreateDirectory(Path.Combine(input, "not-a-platform"));

        await Assert.ThrowsAsync<PackagingException>(() => ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
        }));
    }

    [Fact]
    public async Task The_tar_marks_the_linux_binary_executable_even_when_packed_on_windows()
    {
        using var temp = new TempDirectory("pack-execbit");
        var input = temp.Combine("Build");
        FakePlayerOutput(Path.Combine(input, PlatformKey.LinuxX64), PlatformKey.LinuxX64);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
        });

        var archive = result.Platforms.Single().ArchivePath;
        var modes = await ReadTarModes(archive);

        // A Windows filesystem has no execute bit to copy, so the packer has to set it explicitly.
        // Getting this wrong is exactly how a Linux build ends up refusing to start.
        Assert.True(modes["FriWorld.x86_64"].HasFlag(UnixFileMode.UserExecute));
        Assert.False(modes["UnityPlayer.dll"].HasFlag(UnixFileMode.UserExecute));
    }

    [Fact]
    public async Task The_archive_has_no_wrapping_folder()
    {
        using var temp = new TempDirectory("pack-noroot");
        var input = temp.Combine("Build");
        FakePlayerOutput(Path.Combine(input, PlatformKey.WindowsX64), PlatformKey.WindowsX64);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "1.0.0",
        });

        var extracted = temp.Combine("extracted");
        await ArchiveExtractors.For(ArchiveFormat.Zip)
            .ExtractAsync(result.Platforms.Single().ArchivePath, extracted, null, CancellationToken.None);

        // exec is relative to the install root, so a wrapping folder would break every path.
        Assert.True(File.Exists(Path.Combine(extracted, "FriWorld.exe")));
    }

    [Fact]
    public async Task A_packed_release_installs_through_the_launcher_unchanged()
    {
        using var temp = new TempDirectory("pack-roundtrip");
        var input = temp.Combine("Build");
        FakePlayerOutput(Path.Combine(input, PlatformKey.Current), PlatformKey.Current);

        var result = await ReleasePacker.PackAsync(new ReleasePacker.Options
        {
            InputDirectory = input,
            OutputDirectory = temp.Combine("dist"),
            Version = "0.1.2-alpha",
        });

        var orchestrator = LauncherConfiguration
            .Resolve(result.ManifestPath, temp.Combine("root"))
            .CreateOrchestrator();

        var check = await orchestrator.EnsureLatestAsync();

        Assert.Equal(UpdateReason.NotInstalled, check.Reason);

        var installed = orchestrator.State.Read()!;
        Assert.Equal("0.1.2-alpha", installed.Version);
        Assert.True(File.Exists(Path.Combine(orchestrator.Paths.Game, installed.Exec)));
    }

    private static async Task<Dictionary<string, UnixFileMode>> ReadTarModes(string archivePath)
    {
        var modes = new Dictionary<string, UnixFileMode>(StringComparer.Ordinal);

        await using var file = File.OpenRead(archivePath);
        await using var gzip = new System.IO.Compression.GZipStream(
            file, System.IO.Compression.CompressionMode.Decompress);
        await using var reader = new System.Formats.Tar.TarReader(gzip);

        while (await reader.GetNextEntryAsync() is { } entry)
        {
            modes[entry.Name] = entry.Mode;
        }

        return modes;
    }
}
