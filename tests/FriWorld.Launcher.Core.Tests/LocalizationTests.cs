using System.Reflection;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Install;
using FriWorld.Launcher.Core.Localization;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Update;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The window speaks Slovak or English, and never both at once.
///
/// Most of this is about the second half of that sentence. A launcher with a missing translation
/// does not crash and does not look broken in the language anyone tests — it quietly shows one
/// Slovak line to someone reading English, which is exactly the class of bug nobody reports.
/// </summary>
public class LocalizationTests
{
    private const string SlovakOnlyLetters = "áäčďéíĺľňóôŕšťúýžÁÄČĎÉÍĹĽŇÓÔŔŠŤÚÝŽ";

    /// <summary>Every text with no arguments, read out of both languages by reflection.</summary>
    private static IEnumerable<PropertyInfo> PlainTexts =>
        typeof(Texts)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0);

    [Fact]
    public void Every_text_says_something_in_both_languages()
    {
        var empty = PlainTexts
            .Where(p => string.IsNullOrWhiteSpace((string?)p.GetValue(Texts.Slovak))
                     || string.IsNullOrWhiteSpace((string?)p.GetValue(Texts.English)))
            .Select(p => p.Name)
            .ToList();

        Assert.True(empty.Count == 0, "blank in one language: " + string.Join(", ", empty));
    }

    [Fact]
    public void The_english_texts_are_actually_english()
    {
        // A Slovak letter in an English string means a translation was skipped and the original
        // copied over. The language switch is the one deliberate exception: it names the language
        // it switches to, in that language's own spelling.
        var slovakLeftIn = PlainTexts
            .Where(p => p.Name != nameof(Texts.SwitchLanguageTo))
            .Where(p => ((string)p.GetValue(Texts.English)!).Any(SlovakOnlyLetters.Contains))
            .Select(p => p.Name)
            .ToList();

        Assert.True(slovakLeftIn.Count == 0, "Slovak left in English: " + string.Join(", ", slovakLeftIn));
    }

    [Fact]
    public void The_two_languages_do_not_share_wording_by_accident()
    {
        // Identical strings are legitimate — "Launcher" is "Launcher" — but a whole sentence that
        // came out the same in both is a copied line, not a translation.
        var sameLongText = PlainTexts
            .Select(p => (p.Name, Slovak: (string)p.GetValue(Texts.Slovak)!, English: (string)p.GetValue(Texts.English)!))
            .Where(t => t.Slovak == t.English && t.Slovak.Contains(' '))
            .Select(t => t.Name)
            .ToList();

        Assert.True(sameLongText.Count == 0, "same in both languages: " + string.Join(", ", sameLongText));
    }

    [Theory]
    [InlineData(UpdateStage.CheckingForUpdate)]
    [InlineData(UpdateStage.UpToDate)]
    [InlineData(UpdateStage.Downloading)]
    [InlineData(UpdateStage.Verifying)]
    [InlineData(UpdateStage.Extracting)]
    [InlineData(UpdateStage.Installing)]
    [InlineData(UpdateStage.Ready)]
    [InlineData(UpdateStage.Launching)]
    [InlineData(UpdateStage.Failed)]
    public void Every_stage_a_player_can_see_is_named_in_both_languages(UpdateStage stage)
    {
        Assert.False(string.IsNullOrWhiteSpace(Texts.Slovak.Stage(stage, "1.0")));
        Assert.False(string.IsNullOrWhiteSpace(Texts.English.Stage(stage, "1.0")));
    }

    [Fact]
    public void Sizes_carry_the_language_s_decimal_separator()
    {
        // The window used to hard-code the comma, which made every English size wrong.
        const long oneAndAHalfGigabytes = 1_610_612_736;

        Assert.Equal("1,5 GB", Texts.Slovak.Size(oneAndAHalfGigabytes));
        Assert.Equal("1.5 GB", Texts.English.Size(oneAndAHalfGigabytes));
    }

    [Fact]
    public void A_size_for_the_log_stays_english()
    {
        // The log and the console are read by whoever is fixing something, in one language.
        Assert.Equal("1.5 GB", DiskSpace.Format(1_610_612_736));
    }

    [Theory]
    [InlineData("sk", Language.Slovak)]
    [InlineData("SK", Language.Slovak)]
    [InlineData("sk-SK", Language.Slovak)]
    [InlineData("en", Language.English)]
    [InlineData("en-GB", Language.English)]
    [InlineData("English", Language.English)]
    public void A_language_is_read_from_a_code_or_a_culture_name(string value, Language expected) =>
        Assert.Equal(expected, Languages.TryParse(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("de")]
    [InlineData("nonsense")]
    public void Anything_unrecognised_is_no_answer_rather_than_a_guess(string? value) =>
        Assert.Null(Languages.TryParse(value));

    [Fact]
    public void The_argument_beats_the_environment_beats_the_remembered_choice_beats_the_file()
    {
        Assert.Equal(
            Language.English,
            LauncherConfiguration.ResolveLanguage("en", "sk", Language.Slovak, "sk"));

        Assert.Equal(
            Language.English,
            LauncherConfiguration.ResolveLanguage(null, "en", Language.Slovak, "sk"));

        Assert.Equal(
            Language.English,
            LauncherConfiguration.ResolveLanguage(null, null, Language.English, "sk"));

        Assert.Equal(
            Language.English,
            LauncherConfiguration.ResolveLanguage(null, null, null, "en"));
    }

    [Fact]
    public void Nothing_configured_means_slovak()
    {
        // The audience is Slovak schoolchildren. This is the one default that is not a toss-up.
        Assert.Equal(Language.Slovak, LauncherConfiguration.ResolveLanguage(null, null, null, null));
        Assert.Equal(Language.Slovak, Languages.Default);
    }

    [Fact]
    public void A_chosen_language_survives_a_restart()
    {
        using var temp = new TempDirectory("language-remembered");
        var paths = new LauncherPaths(temp.Combine("root"));

        Assert.Null(LanguagePreference.Read(paths));

        LanguagePreference.Write(paths, Language.English);

        Assert.Equal(Language.English, LanguagePreference.Read(paths));
    }

    [Fact]
    public void An_unreadable_preference_is_not_a_reason_to_refuse_to_start()
    {
        using var temp = new TempDirectory("language-nonsense");
        var paths = new LauncherPaths(temp.Combine("root"));

        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(Path.Combine(paths.Root, LanguagePreference.FileName), "klingon");

        Assert.Null(LanguagePreference.Read(paths));
    }

    [Fact]
    public void The_settings_file_can_start_a_machine_in_english()
    {
        using var temp = new TempDirectory("settings-language");
        File.WriteAllText(
            temp.Combine(LauncherSettingsFile.FileName),
            """
            {
              "language": "en"
            }
            """);

        Assert.Equal("en", LauncherSettingsFile.Load(temp.Path).Language);
    }

    [Fact]
    public void An_english_release_note_is_shown_when_the_manifest_carries_one()
    {
        var manifest = new ReleaseManifest
        {
            Notes = "Pridali sme knižnicu.",
            NotesEn = "We added the library.",
        };

        Assert.Equal("Pridali sme knižnicu.", manifest.NotesFor(Language.Slovak));
        Assert.Equal("We added the library.", manifest.NotesFor(Language.English));
    }

    [Fact]
    public void An_english_window_falls_back_to_the_slovak_note_rather_than_showing_nothing()
    {
        // Every manifest written before this feature existed has only the Slovak note, and a
        // release note in the wrong language beats an empty "What's new".
        var manifest = new ReleaseManifest { Notes = "Pridali sme knižnicu." };
        var launcher = new LauncherRelease { Notes = "Okno vie po anglicky." };

        Assert.Equal("Pridali sme knižnicu.", manifest.NotesFor(Language.English));
        Assert.Equal("Okno vie po anglicky.", launcher.NotesFor(Language.English));
    }

    [Fact]
    public void A_manifest_without_an_english_note_still_parses()
    {
        // Older manifests have no notesEn at all; newer launchers must read them unchanged.
        var manifest = ManifestJson.Parse(
            """
            {
              "version": "1.0.0",
              "notes": "Prvá verzia.",
              "platforms": {
                "win-x64": {
                  "url": "https://friworld.example/game.zip",
                  "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
                  "size": 10,
                  "exec": "FriWorld.exe"
                }
              }
            }
            """);

        Assert.Null(manifest.NotesEn);
        Assert.Equal("Prvá verzia.", manifest.NotesFor(Language.English));
    }

    [Fact]
    public void An_english_note_survives_a_round_trip_through_the_manifest()
    {
        var written = ManifestJson.Write(new ReleaseManifest
        {
            Version = "1.0.0",
            Notes = "Prvá verzia.",
            NotesEn = "First version.",
            Platforms = new Dictionary<string, PlatformPackage>
            {
                ["win-x64"] = new()
                {
                    Url = "https://friworld.example/game.zip",
                    Sha256 = new string('0', 64),
                    Size = 10,
                    Exec = "FriWorld.exe",
                },
            },
        });

        Assert.Contains("\"notesEn\"", written, StringComparison.Ordinal);
        Assert.Equal("First version.", ManifestJson.Parse(written).NotesFor(Language.English));
    }

    [Fact]
    public void The_switch_offers_the_language_it_is_not_showing()
    {
        Assert.Equal("English", Texts.Slovak.SwitchLanguageTo);
        Assert.Equal("Slovenčina", Texts.English.SwitchLanguageTo);
    }
}
