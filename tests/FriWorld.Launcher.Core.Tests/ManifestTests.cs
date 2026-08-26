using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Tests;

public class ManifestTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static string ValidJson(string version = "0.1.2-alpha") =>
        $$"""
        {
          "version": "{{version}}",
          "released": "2026-08-26T10:00:00Z",
          "notes": "Something changed.",
          "platforms": {
            "win-x64": {
              "archive": "ignored-unknown-field.zip",
              "url": "https://example.test/FriWorld-win-x64.zip",
              "sha256": "{{ValidSha}}",
              "size": 812934144,
              "exec": "FriWorld.exe"
            },
            "linux-x64": {
              "url": "https://example.test/FriWorld-linux-x64.tar.gz",
              "sha256": "{{ValidSha}}",
              "size": 800000000,
              "exec": "FriWorld"
            }
          }
        }
        """;

    [Fact]
    public void Parses_a_well_formed_manifest()
    {
        var manifest = ManifestJson.Parse(ValidJson());

        Assert.Equal("0.1.2-alpha", manifest.Version);
        Assert.Equal("Something changed.", manifest.Notes);
        Assert.Equal(2, manifest.Platforms.Count);
        Assert.Equal("FriWorld.exe", manifest.Platforms["win-x64"].Exec);
    }

    [Fact]
    public void Ignores_fields_it_does_not_know()
    {
        // The build pipeline must be free to add fields without breaking launchers already shipped.
        var manifest = ManifestJson.Parse(ValidJson());

        Assert.Equal(812934144, manifest.Platforms["win-x64"].Size);
    }

    [Fact]
    public void Infers_the_archive_format_from_the_url()
    {
        var manifest = ManifestJson.Parse(ValidJson());

        Assert.Equal(ArchiveFormat.Zip, manifest.Platforms["win-x64"].ResolvedFormat);
        Assert.Equal(ArchiveFormat.TarGz, manifest.Platforms["linux-x64"].ResolvedFormat);
    }

    [Theory]
    [InlineData("https://example.test/a.zip?X-Amz-Signature=deadbeef", ArchiveFormat.Zip)]
    [InlineData("https://example.test/a.tar.gz?token=1#frag", ArchiveFormat.TarGz)]
    [InlineData("a.tgz", ArchiveFormat.TarGz)]
    public void Infers_the_format_past_query_strings(string url, ArchiveFormat expected)
    {
        // Signed download URLs carry a query string, which must not hide the extension.
        Assert.Equal(expected, ArchiveFormats.InferFrom(url));
    }

    [Fact]
    public void Accepts_a_relative_archive_url()
    {
        // Validation runs before the source resolves urls against the manifest's own location,
        // so demanding an absolute url here would reject every manifest that names files beside it.
        var json = ValidJson().Replace(
            "https://example.test/FriWorld-win-x64.zip", "FriWorld-win-x64.zip", StringComparison.Ordinal);

        var manifest = ManifestJson.Parse(json);

        Assert.Equal("FriWorld-win-x64.zip", manifest.Platforms["win-x64"].Url);
    }

    [Fact]
    public void Rejects_a_platform_with_an_empty_url()
    {
        var json = ValidJson().Replace(
            "https://example.test/FriWorld-win-x64.zip", "", StringComparison.Ordinal);

        Assert.Throws<ManifestException>(() => ManifestJson.Parse(json));
    }

    [Fact]
    public void Derived_fields_stay_out_of_the_written_json()
    {
        // Writing them back would put fields into the manifest the build pipeline never set.
        var written = ManifestJson.Write(ManifestJson.Parse(ValidJson()));

        Assert.DoesNotContain("cacheFileName", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resolvedFormat", written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_a_manifest_with_no_version() =>
        Assert.Throws<ManifestException>(() => ManifestJson.Parse(ValidJson(version: "")));

    [Fact]
    public void Rejects_a_truncated_checksum()
    {
        var json = ValidJson().Replace(ValidSha, "abc123", StringComparison.Ordinal);

        var error = Assert.Throws<ManifestException>(() => ManifestJson.Parse(json));
        Assert.Contains("sha256", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_a_platform_with_no_exec()
    {
        var json = ValidJson().Replace("\"exec\": \"FriWorld.exe\"", "\"exec\": \"\"", StringComparison.Ordinal);

        Assert.Throws<ManifestException>(() => ManifestJson.Parse(json));
    }

    [Fact]
    public void Picks_the_first_matching_platform_key()
    {
        var manifest = ManifestJson.Parse(ValidJson());

        Assert.True(manifest.TryGetPackage(["osx-arm64", "linux-x64"], out var key, out _));
        Assert.Equal("linux-x64", key);
    }

    [Fact]
    public void Reports_no_match_when_the_release_skips_this_platform()
    {
        var manifest = ManifestJson.Parse(ValidJson());

        Assert.False(manifest.TryGetPackage(["osx-arm64"], out _, out _));
    }
}
