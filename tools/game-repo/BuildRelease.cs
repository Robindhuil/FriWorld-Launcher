using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the desktop players the launcher distributes, into
/// <c>Build/&lt;bundleVersion&gt;/&lt;platformKey&gt;/</c>.
///
/// This deliberately stops at the players. Archiving, checksums and the manifest are done
/// afterwards by <c>FriWorld.Launcher.Cli pack</c>, for three reasons. Unity runs on a framework
/// with no tar writer at all. A tar written on Windows records no execute bit, because a Windows
/// filesystem has none, so a Linux build packed here would refuse to start. And the manifest is
/// the contract between this repository and the launcher's — a contract with two independent
/// implementations drifts, so only one side writes it.
///
/// The folder names are not cosmetic: they are the platform keys the launcher looks up in the
/// manifest, and <c>pack</c> reads them straight off the directory names.
///
/// <c>bundleVersion</c> is read and never written. Raising it is a deliberate act by a person,
/// and it is the single point that decides what a release is called.
/// </summary>
public static class BuildRelease
{
    private const string MenuRoot = "FriWorld/Build/";
    private const string IncludeLinuxMenu = MenuRoot + "Include Linux";
    private const string IncludeLinuxPref = "FriWorld.Build.IncludeLinux";

    /// <summary>Platform keys shared with the launcher's manifest. Do not rename casually.</summary>
    private const string WindowsKey = "win-x64";
    private const string LinuxKey = "linux-x64";

    [MenuItem(MenuRoot + "Release", priority = 0)]
    public static void BuildAll()
    {
        var version = PlayerSettings.bundleVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            Fail("bundleVersion is empty. Set it in Project Settings before building a release.");
            return;
        }

        var scenes = EnabledScenes();

        if (scenes.Length == 0)
        {
            Fail("No scenes are enabled in Build Settings. The build would produce an empty player.");
            return;
        }

        var targets = new List<(string Key, BuildTarget Target)> { (WindowsKey, BuildTarget.StandaloneWindows64) };

        if (IncludeLinux)
        {
            targets.Add((LinuxKey, BuildTarget.StandaloneLinux64));
        }

        foreach (var (key, target) in targets)
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                continue;
            }

            Fail(
                $"The build module for {target} is not installed, so {key} cannot be built.\n\n" +
                "Unity Hub → Installs → the version's ⚙ → Add modules.\n\n" +
                (target == BuildTarget.StandaloneLinux64
                    ? "Or turn off " + IncludeLinuxMenu + " and ship Windows only."
                    : string.Empty));
            return;
        }

        var outputRoot = Path.Combine(ProjectRoot, "Build", version);
        var summary = new StringBuilder();
        summary.AppendLine($"FriWorld {version}");
        summary.AppendLine();

        var started = DateTime.Now;

        foreach (var (key, target) in targets)
        {
            var playerDirectory = Path.Combine(outputRoot, key);

            // A previous run's files would otherwise be packed alongside this one's.
            if (Directory.Exists(playerDirectory))
            {
                Directory.Delete(playerDirectory, recursive: true);
            }

            Directory.CreateDirectory(playerDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(playerDirectory, ExecutableNameFor(target)),
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            Debug.Log($"[BuildRelease] Building {key} into {playerDirectory}");

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail(
                    $"The {key} build did not succeed: {report.summary.result}, " +
                    $"{report.summary.totalErrors} error(s). See the Console.");
                return;
            }

            summary.AppendLine(
                $"{key,-12} {Megabytes(report.summary.totalSize),8}  " +
                $"{report.summary.totalTime:mm\\:ss}");
        }

        summary.AppendLine();
        summary.AppendLine($"Total {DateTime.Now - started:mm\\:ss}");
        summary.AppendLine();
        summary.AppendLine("Next, pack it from the launcher repository:");
        summary.AppendLine();
        summary.AppendLine(PackCommand(outputRoot, version));

        Debug.Log("[BuildRelease] " + summary);
        EditorUtility.DisplayDialog("Release built", summary.ToString(), "OK");
        EditorUtility.RevealInFinder(outputRoot);
    }

    /// <summary>
    /// Linux is optional because the audience is Slovak schools, where the machines are Windows,
    /// and everything else is covered by the web build. The launcher and the packer both handle
    /// Linux already, so turning this on later costs a build module and nothing else.
    /// </summary>
    [MenuItem(IncludeLinuxMenu, priority = 20)]
    private static void ToggleIncludeLinux() => IncludeLinux = !IncludeLinux;

    [MenuItem(IncludeLinuxMenu, validate = true)]
    private static bool ToggleIncludeLinuxValidate()
    {
        Menu.SetChecked(IncludeLinuxMenu, IncludeLinux);
        return true;
    }

    private static bool IncludeLinux
    {
        get => EditorPrefs.GetBool(IncludeLinuxPref, false);
        set => EditorPrefs.SetBool(IncludeLinuxPref, value);
    }

    private static string[] EnabledScenes() => EditorBuildSettings.scenes
        .Where(scene => scene.enabled)
        .Select(scene => scene.path)
        .ToArray();

    /// <summary>
    /// Unity decides the binary's name from this path, and the two platforms disagree about it:
    /// Windows wants <c>&lt;name&gt;.exe</c>, Linux wants <c>&lt;name&gt;.x86_64</c>. Getting the
    /// Linux one wrong produces a player that exists but that nothing knows how to start.
    /// </summary>
    private static string ExecutableNameFor(BuildTarget target)
    {
        var name = new string(PlayerSettings.productName.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (string.IsNullOrEmpty(name))
        {
            name = "FriWorld";
        }

        return target == BuildTarget.StandaloneLinux64 ? name + ".x86_64" : name + ".exe";
    }

    private static string PackCommand(string outputRoot, string version) =>
        "dotnet run --project src/FriWorld.Launcher.Cli -- pack \\\n" +
        $"  --input \"{outputRoot.Replace('\\', '/')}\" \\\n" +
        $"  --version {version}";

    /// <summary>The folder holding Assets, so Build/ lands beside it rather than inside it.</summary>
    private static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

    private static string Megabytes(ulong bytes) => $"{bytes / 1024d / 1024d:0.#} MB";

    private static void Fail(string message)
    {
        Debug.LogError("[BuildRelease] " + message);
        EditorUtility.DisplayDialog("Release build stopped", message, "OK");
    }
}
