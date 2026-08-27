using System.Runtime.CompilerServices;
using FriWorld.Launcher.Core;
using FriWorld.Launcher.Core.Mock;
using FriWorld.Launcher.Core.Platform;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// Points the window at a throwaway install root and a mock release. The view model reads both
/// from the environment the moment it is constructed, so this has to be in place before the first
/// window exists — otherwise the suite would take the real single-instance lock and read the real
/// installation.
/// </summary>
internal static class WindowSandbox
{
    private static readonly string Home = Path.Combine(
        Path.GetTempPath(),
        "friworld-window-tests",
        Guid.NewGuid().ToString("N")[..8]);

    private static int _counter;

    /// <summary>The mock release every window in this suite is pointed at.</summary>
    internal static string Manifest { get; private set; } = string.Empty;

    [ModuleInitializer]
    internal static void BuildTheMockRelease()
    {
        // The stub stays alive for a couple of seconds, so a test can watch the launcher go away
        // while it runs and come back when it stops.
        Manifest = MockReleaseBuilder.BuildAsync(
            Path.Combine(Home, "store"),
            new MockReleaseBuilder.Options
            {
                Version = "1.0.0-mock",
                PayloadBytes = 4096,
                Platforms = [PlatformKey.Current],
                StubRunsForSeconds = 2,
            }).GetAwaiter().GetResult();

        Environment.SetEnvironmentVariable(LauncherConfiguration.ManifestUrlVariable, Manifest);
        FreshInstallRoot();
    }

    /// <summary>The install root the next window will use.</summary>
    internal static string CurrentInstallRoot { get; private set; } = string.Empty;

    /// <summary>
    /// Gives the next window its own install root, and with it its own single-instance lock.
    ///
    /// Nothing releases that lock between tests — the view model holds it for the life of the
    /// process — so sharing a root would leave every window after the first reporting that another
    /// launcher is running, and the tests would quietly be exercising a state nobody meant.
    /// </summary>
    internal static void FreshInstallRoot()
    {
        CurrentInstallRoot = Path.Combine(Home, $"install-{Interlocked.Increment(ref _counter)}");
        Environment.SetEnvironmentVariable(LauncherPaths.RootOverrideVariable, CurrentInstallRoot);
    }
}
