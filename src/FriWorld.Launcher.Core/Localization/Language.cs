namespace FriWorld.Launcher.Core.Localization;

/// <summary>
/// The languages the launcher speaks to a player.
///
/// Slovak is first because it is the default, and the default is not a toss-up: the game is built
/// for Slovak schoolchildren and that is who opens this window. English exists for everyone else —
/// a visitor at an open day, a lecturer showing the game to a foreign guest.
///
/// Two is the whole list on purpose. Every string in <see cref="Texts"/> carries both languages on
/// one line, which is what makes a missing translation impossible rather than merely unlikely; a
/// third language would be a different shape, and is a change to make when it is actually needed.
/// </summary>
public enum Language
{
    Slovak,
    English,
}

public static class Languages
{
    /// <summary>What a player gets when nothing has said otherwise.</summary>
    public const Language Default = Language.Slovak;

    /// <summary>The two-letter code, as written in <c>launcher.json</c> and in the remembered choice.</summary>
    public static string Code(this Language language) => language == Language.English ? "en" : "sk";

    /// <summary>The language's own name for itself, for the switch in the window.</summary>
    public static string NativeName(this Language language) =>
        language == Language.English ? "English" : "Slovenčina";

    /// <summary>
    /// Reads a language out of configuration. Returns null for anything unrecognised, including
    /// null and blank, so the caller can fall through to the next source rather than guess.
    ///
    /// A culture name is accepted as well as a bare code, because <c>sk-SK</c> is what anyone who
    /// has met .NET will type, and refusing it would be pedantry with no upside.
    /// </summary>
    public static Language? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var dash = trimmed.IndexOf('-');
        var prefix = dash < 0 ? trimmed : trimmed[..dash];

        return prefix.ToLowerInvariant() switch
        {
            "sk" or "slovak" or "slovencina" or "slovenčina" => Language.Slovak,
            "en" or "english" or "anglictina" or "angličtina" => Language.English,
            _ => null,
        };
    }
}
