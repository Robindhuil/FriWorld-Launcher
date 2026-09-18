using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Localization;

/// <summary>
/// The language someone chose in the window, remembered between runs.
///
/// It is deliberately not kept in <c>launcher.json</c>. That file belongs to the deployment — it
/// is what a teacher or a release writes to say where the manifest lives — and it can sit in a
/// folder the launcher is not allowed to write to. This belongs to the person at the keyboard, so
/// it lives with the rest of their state, beside <c>installed.json</c>.
///
/// One line, no JSON: a file whose whole content is <c>sk</c> or <c>en</c> is something anyone can
/// read and fix, and there is nothing here that a second field would want to join.
/// </summary>
public static class LanguagePreference
{
    public const string FileName = "language.txt";

    /// <summary>The remembered choice, or null when nobody has made one.</summary>
    public static Language? Read(LauncherPaths paths)
    {
        try
        {
            var path = Path.Combine(paths.Root, FileName);
            return File.Exists(path) ? Languages.TryParse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A language nobody can read is not a reason to refuse to start.
            return null;
        }
    }

    /// <summary>
    /// Remembers the choice. Silent on failure by design: the switch has already changed the
    /// window, and an error about a preference file would be the least useful sentence the
    /// launcher could put on screen.
    /// </summary>
    public static void Write(LauncherPaths paths, Language language)
    {
        try
        {
            Directory.CreateDirectory(paths.Root);
            File.WriteAllText(Path.Combine(paths.Root, FileName), language.Code());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to do and nothing worth saying.
        }
    }
}
