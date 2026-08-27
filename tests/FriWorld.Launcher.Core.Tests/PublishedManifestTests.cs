using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The manifest in <c>releases/</c> is the one real launchers read. It is normally written by
/// <c>launcher pack</c>, which reads it straight back — but when the game build is no longer on
/// disk the launcher section gets edited by hand, and then nothing checks it. These tests are
/// that check: a manifest this repo publishes has to survive the launcher's own reader.
/// </summary>
public class PublishedManifestTests
{
    private static string PublishedManifestPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "releases", "manifest.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("releases/manifest.json was not found above the test binary.");
    }

    private static ReleaseManifest Published() => ManifestJson.Parse(File.ReadAllText(PublishedManifestPath()));

    [Fact]
    public void The_published_manifest_is_readable()
    {
        var manifest = Published();

        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.NotEmpty(manifest.Platforms);
    }

    [Fact]
    public void The_published_manifest_passes_its_own_validation()
    {
        // Same call the launcher makes before it trusts anything in here.
        Published().Validate();
    }

    [Fact]
    public void Every_published_url_is_absolute()
    {
        // Relative urls resolve against wherever the manifest is served from. This one is served
        // from raw.githubusercontent.com, where nothing else lives, so they must be absolute.
        var manifest = Published();

        foreach (var (key, platform) in manifest.Platforms)
        {
            Assert.True(
                Uri.TryCreate(platform.Url, UriKind.Absolute, out _),
                $"platforms.{key}.url is not absolute: {platform.Url}");
        }
    }

    [Fact]
    public void The_launcher_binary_is_served_over_https()
    {
        // The launcher replaces itself with this file, so the rule is stricter than for the game:
        // a manifest from a tampered connection must not be able to name an executable.
        var launcher = Published().Launcher;

        if (launcher?.Platforms is null)
        {
            return;
        }

        foreach (var (key, binary) in launcher.Platforms)
        {
            Assert.True(
                Uri.TryCreate(binary.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                $"launcher.platforms.{key}.url is not https: {binary.Url}");
        }
    }
}
