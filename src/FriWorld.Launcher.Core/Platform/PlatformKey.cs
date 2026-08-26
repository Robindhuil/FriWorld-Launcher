using System.Runtime.InteropServices;

namespace FriWorld.Launcher.Core.Platform;

/// <summary>
/// Identifies a build target. These strings are the keys of the manifest's <c>platforms</c> map,
/// so they are part of the contract with the game's build pipeline and must not be renamed casually.
/// </summary>
public static class PlatformKey
{
    public const string WindowsX64 = "win-x64";
    public const string LinuxX64 = "linux-x64";
    public const string MacArm64 = "osx-arm64";
    public const string MacX64 = "osx-x64";

    /// <summary>The key matching the machine the launcher is running on.</summary>
    public static string Current { get; } = Resolve();

    /// <summary>
    /// Keys the current machine can run, best match first. An Apple Silicon Mac can run an
    /// x64 build through Rosetta, so it accepts <c>osx-x64</c> as a fallback. Nothing else falls back.
    /// </summary>
    public static IReadOnlyList<string> CurrentWithFallbacks { get; } = ResolveWithFallbacks();

    private static string Resolve()
    {
        var arch = RuntimeInformation.OSArchitecture;

        if (OperatingSystem.IsWindows())
        {
            return arch is Architecture.X64 or Architecture.X86
                ? WindowsX64
                : throw new PlatformNotSupportedException($"Windows on {arch} has no build target.");
        }

        if (OperatingSystem.IsLinux())
        {
            return arch is Architecture.X64
                ? LinuxX64
                : throw new PlatformNotSupportedException($"Linux on {arch} has no build target.");
        }

        if (OperatingSystem.IsMacOS())
        {
            return arch switch
            {
                Architecture.Arm64 => MacArm64,
                Architecture.X64 => MacX64,
                _ => throw new PlatformNotSupportedException($"macOS on {arch} has no build target."),
            };
        }

        throw new PlatformNotSupportedException(
            $"Unsupported operating system: {RuntimeInformation.OSDescription}");
    }

    private static IReadOnlyList<string> ResolveWithFallbacks() =>
        Current == MacArm64 ? [MacArm64, MacX64] : [Current];
}
