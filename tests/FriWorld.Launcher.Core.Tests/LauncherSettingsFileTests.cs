using FriWorld.Launcher.Core;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The settings file beside the executable. It exists so the manifest address is not baked into
/// the binary — moving the release storage should be an edit, not a new build shipped to everyone.
/// </summary>
public class LauncherSettingsFileTests
{
    [Fact]
    public void No_file_means_no_settings()
    {
        using var temp = new TempDirectory("settings-absent");

        var settings = LauncherSettingsFile.Load(temp.Path);

        Assert.Null(settings.ManifestUrl);
        Assert.Null(settings.InstallRoot);
    }

    [Fact]
    public void Reads_both_fields()
    {
        using var temp = new TempDirectory("settings-full");
        File.WriteAllText(
            temp.Combine(LauncherSettingsFile.FileName),
            """
            {
              "manifestUrl": "https://friworld.example/releases/manifest.json",
              "installRoot": "instalacia"
            }
            """);

        var settings = LauncherSettingsFile.Load(temp.Path);

        Assert.Equal("https://friworld.example/releases/manifest.json", settings.ManifestUrl);
        Assert.Equal("instalacia", settings.InstallRoot);
    }

    [Fact]
    public void A_partial_file_is_fine()
    {
        using var temp = new TempDirectory("settings-partial");
        File.WriteAllText(
            temp.Combine(LauncherSettingsFile.FileName),
            """{ "manifestUrl": "https://friworld.example/m.json" }""");

        var settings = LauncherSettingsFile.Load(temp.Path);

        Assert.Equal("https://friworld.example/m.json", settings.ManifestUrl);
        Assert.Null(settings.InstallRoot);
    }

    [Fact]
    public void A_broken_file_does_not_stop_the_launcher()
    {
        // Refusing to start would leave nobody able to say what went wrong. Falling back to the
        // defaults at least gets a window on screen, and the failure shows up there.
        using var temp = new TempDirectory("settings-broken");
        File.WriteAllText(temp.Combine(LauncherSettingsFile.FileName), "{ not json at all");

        var settings = LauncherSettingsFile.Load(temp.Path);

        Assert.Null(settings.ManifestUrl);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        using var temp = new TempDirectory("settings-extra");
        File.WriteAllText(
            temp.Combine(LauncherSettingsFile.FileName),
            """{ "manifestUrl": "https://a.test/m.json", "somethingAddedLater": 42 }""");

        Assert.Equal("https://a.test/m.json", LauncherSettingsFile.Load(temp.Path).ManifestUrl);
    }
}
