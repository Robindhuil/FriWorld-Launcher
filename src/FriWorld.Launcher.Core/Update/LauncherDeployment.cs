using System.Reflection;

namespace FriWorld.Launcher.Core.Update;

/// <summary>
/// How this launcher is deployed: which file it runs from, and whether that file is the whole
/// application.
///
/// It exists as a value rather than as static lookups so the self-update can be exercised against
/// real files in a test. That matters more here than anywhere else in the project: the swap is
/// the one operation that can leave someone with nothing that runs, and "never actually tested"
/// was not an acceptable place to leave it.
/// </summary>
/// <param name="ExecutablePath">The file this process runs from, or null when it cannot be told.</param>
/// <param name="IsSingleFile">Whether that one file is the entire application.</param>
public sealed record LauncherDeployment(string? ExecutablePath, bool IsSingleFile)
{
    /// <summary>How the running process is actually deployed.</summary>
    public static LauncherDeployment Current { get; } =
        new(Environment.ProcessPath, DetectSingleFile());

    /// <summary>
    /// A single-file bundle reports no assembly location, because nothing was extracted to disk
    /// to have one. The analyser warns about reading Location in a single-file app; here the
    /// empty answer is exactly the signal being looked for.
    /// </summary>
    private static bool DetectSingleFile()
    {
#pragma warning disable IL3000
        return string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);
#pragma warning restore IL3000
    }
}
