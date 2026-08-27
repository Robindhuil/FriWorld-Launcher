using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Packaging;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// Releasing the launcher on its own means editing a manifest whose game section must come out
/// untouched. These tests are about what survives the edit, not about what it writes.
/// </summary>
public class LauncherSectionWriterTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string Existing = $$"""
        {
          "version": "0.1.1-alpha",
          "released": "2026-08-26T10:00:00Z",
          "notes": "Prva alfa.",
          "somethingNewer": { "keep": "me" },
          "platforms": {
            "win-x64": {
              "url": "https://example.test/FriWorld-win-x64.zip",
              "sha256": "{{ValidSha}}",
              "size": 435666845,
              "exec": "FriWorld.exe"
            }
          },
          "launcher": {
            "version": "0.1.2-alpha",
            "downloadUrl": "https://example.test/stiahnut",
            "platforms": {
              "win-x64": {
                "url": "https://example.test/v0.1.2/FriWorldLauncher.exe",
                "sha256": "{{ValidSha}}",
                "size": 52755047
              }
            }
          }
        }
        """;

    private static LauncherRelease Release(string version = "0.1.3-alpha") => new()
    {
        Version = version,
        DownloadUrl = "https://example.test/stiahnut",
        Notes = "Nieco nove.",
        Platforms = new Dictionary<string, LauncherBinary>
        {
            ["win-x64"] = new()
            {
                Url = $"https://example.test/v{version}/FriWorldLauncher.exe",
                Sha256 = ValidSha,
                Size = 52763618,
            },
        },
    };

    private static string WriteExisting(TempDirectory temp, string json = Existing)
    {
        var path = Path.Combine(temp.Path, "manifest.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Replaces_the_launcher_section()
    {
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);

        LauncherSectionWriter.Write(path, Release());

        var manifest = ManifestJson.Parse(File.ReadAllText(path));
        Assert.Equal("0.1.3-alpha", manifest.Launcher!.Version);
        Assert.Equal(52763618, manifest.Launcher.Platforms["win-x64"].Size);
    }

    [Fact]
    public void Leaves_the_game_alone()
    {
        // The whole point of the mode: a launcher release is not a game release.
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);

        LauncherSectionWriter.Write(path, Release());

        var manifest = ManifestJson.Parse(File.ReadAllText(path));
        Assert.Equal("0.1.1-alpha", manifest.Version);
        Assert.Equal("Prva alfa.", manifest.Notes);
        Assert.Equal(435666845, manifest.Platforms["win-x64"].Size);
    }

    [Fact]
    public void Keeps_fields_it_does_not_understand()
    {
        // Manifests are allowed to carry fields this launcher has never heard of. Tolerating them
        // on read is worth nothing if the tooling drops them on write.
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);

        LauncherSectionWriter.Write(path, Release());

        Assert.Contains("somethingNewer", File.ReadAllText(path));
    }

    [Fact]
    public void Removes_the_section_when_asked()
    {
        // The safe rollback: without a launcher section the launcher only offers a download page.
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);

        LauncherSectionWriter.Write(path, null);

        var manifest = ManifestJson.Parse(File.ReadAllText(path));
        Assert.Null(manifest.Launcher);
        Assert.Equal("0.1.1-alpha", manifest.Version);
    }

    [Fact]
    public void Refuses_a_binary_that_is_not_on_https()
    {
        // The launcher replaces itself with this file, so http is not a judgement call.
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);
        var before = File.ReadAllText(path);

        var release = Release() with
        {
            Platforms = new Dictionary<string, LauncherBinary>
            {
                ["win-x64"] = new()
                {
                    Url = "http://example.test/FriWorldLauncher.exe",
                    Sha256 = ValidSha,
                    Size = 10,
                },
            },
        };

        Assert.ThrowsAny<Exception>(() => LauncherSectionWriter.Write(path, release));
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void Refuses_a_release_with_no_download_page()
    {
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp);

        var release = Release() with { DownloadUrl = string.Empty };

        Assert.Throws<PackagingException>(() => LauncherSectionWriter.Write(path, release));
    }

    [Fact]
    public void Refuses_to_touch_a_manifest_that_is_not_there()
    {
        using var temp = new TempDirectory("launcher-section");

        Assert.Throws<PackagingException>(() =>
            LauncherSectionWriter.Write(Path.Combine(temp.Path, "nope.json"), Release()));
    }

    [Fact]
    public void Leaves_the_file_as_it_was_when_the_result_would_not_parse()
    {
        // Writing first and validating after would publish a manifest that takes every launcher
        // in the wild down with it.
        using var temp = new TempDirectory("launcher-section");
        var path = WriteExisting(temp, """{ "version": "0.1.1-alpha", "platforms": {} }""");
        var before = File.ReadAllText(path);

        Assert.ThrowsAny<Exception>(() => LauncherSectionWriter.Write(path, Release()));
        Assert.Equal(before, File.ReadAllText(path));
    }
}
