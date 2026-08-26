using System.IO.Compression;
using FriWorld.Launcher.Core.Extract;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Tests;

public class ExtractionTests
{
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("nested/../../escape.txt")]
    [InlineData("/absolute/../../escape.txt")]
    public void Refuses_entries_that_climb_out_of_the_destination(string entryName)
    {
        using var temp = new TempDirectory("slip");
        var destination = temp.Combine("dest");
        Directory.CreateDirectory(destination);

        Assert.Throws<ExtractionException>(() => ExtractionPaths.ResolveInside(destination, entryName));
    }

    [Fact]
    public void Allows_ordinary_nested_entries()
    {
        using var temp = new TempDirectory("nested");
        var destination = temp.Combine("dest");
        Directory.CreateDirectory(destination);

        var resolved = ExtractionPaths.ResolveInside(destination, "FriWorld_Data/level0");

        Assert.StartsWith(Path.GetFullPath(destination), resolved, StringComparison.Ordinal);
        Assert.EndsWith("level0", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zip_extraction_wipes_whatever_was_there_before()
    {
        using var temp = new TempDirectory("zip");

        var payload = temp.Combine("payload");
        Directory.CreateDirectory(payload);
        await File.WriteAllTextAsync(Path.Combine(payload, "new.txt"), "new");

        var archive = temp.Combine("build.zip");
        ZipFile.CreateFromDirectory(payload, archive, CompressionLevel.Fastest, includeBaseDirectory: false);

        var destination = temp.Combine("dest");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "stale.txt"), "left over from a failed run");

        await ArchiveExtractors.For(ArchiveFormat.Zip)
            .ExtractAsync(archive, destination, null, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(destination, "new.txt")));
        Assert.False(File.Exists(Path.Combine(destination, "stale.txt")));
    }

    [Fact]
    public async Task TarGz_round_trips_a_nested_tree()
    {
        using var temp = new TempDirectory("targz");

        var payload = temp.Combine("payload");
        Directory.CreateDirectory(Path.Combine(payload, "FriWorld_Data"));
        await File.WriteAllTextAsync(Path.Combine(payload, "FriWorld"), "#!/bin/sh\necho hi\n");
        await File.WriteAllTextAsync(Path.Combine(payload, "FriWorld_Data", "version.txt"), "1.0");

        var archive = temp.Combine("build.tar.gz");
        await CreateTarGz(payload, archive);

        var destination = temp.Combine("dest");
        await ArchiveExtractors.For(ArchiveFormat.TarGz)
            .ExtractAsync(archive, destination, null, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(destination, "FriWorld")));
        Assert.Equal("1.0", await File.ReadAllTextAsync(Path.Combine(destination, "FriWorld_Data", "version.txt")));
    }

    [Fact]
    public async Task TarGz_preserves_the_execute_bit()
    {
        // Windows has no unix permission bits, so there is nothing to assert there.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory("execbit");

        var payload = temp.Combine("payload");
        Directory.CreateDirectory(payload);
        var binary = Path.Combine(payload, "FriWorld");
        await File.WriteAllTextAsync(binary, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var archive = temp.Combine("build.tar.gz");
        await CreateTarGz(payload, archive);

        var destination = temp.Combine("dest");
        await ArchiveExtractors.For(ArchiveFormat.TarGz)
            .ExtractAsync(archive, destination, null, CancellationToken.None);

        var mode = File.GetUnixFileMode(Path.Combine(destination, "FriWorld"));
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
    }

    private static async Task CreateTarGz(string sourceDirectory, string archivePath)
    {
        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        await System.Formats.Tar.TarFile.CreateFromDirectoryAsync(sourceDirectory, gzip, includeBaseDirectory: false);
    }
}
