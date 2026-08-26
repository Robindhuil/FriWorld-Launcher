using System.Formats.Tar;
using System.IO.Compression;
using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Mock;

/// <summary>
/// Produces a fake release — archives plus a manifest — in a local folder.
///
/// This exists so the launcher can be built and tested today, before the game's build pipeline
/// produces anything. Pointed at the folder this writes, the launcher runs its real path end to
/// end: real download (over <c>file://</c>), real checksum, real extraction with real permission
/// bits, real directory swap, real process start. Only the network is absent, and swapping the
/// URL for an https one is the only change needed when actual storage exists.
/// </summary>
public static class MockReleaseBuilder
{
    /// <summary>Size of the filler payload, big enough that progress and hashing are not instantaneous.</summary>
    public const int DefaultPayloadBytes = 8 * 1024 * 1024;

    public sealed record Options
    {
        public string Version { get; init; } = "0.0.1-mock";

        public string? Notes { get; init; } = "Mock release generated for local development.";

        public int PayloadBytes { get; init; } = DefaultPayloadBytes;

        /// <summary>
        /// How long the stand-in game stays up before closing itself.
        ///
        /// Zero exits immediately, which is what a build crashing on startup looks like — useful,
        /// but it means the launcher never sees a successful launch. Anything longer than the
        /// launch grace period exercises the other half: the version gets confirmed, the previous
        /// install is dropped and the window closes.
        /// </summary>
        public int StubRunsForSeconds { get; init; }

        /// <summary>Platform keys to generate. Defaults to all three real targets.</summary>
        public IReadOnlyList<string> Platforms { get; init; } =
            [PlatformKey.WindowsX64, PlatformKey.LinuxX64, PlatformKey.MacArm64];
    }

    /// <summary>Writes archives and <c>manifest.json</c> into <paramref name="storeDirectory"/> and returns the manifest path.</summary>
    public static async Task<string> BuildAsync(
        string storeDirectory,
        Options? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new Options();

        Directory.CreateDirectory(storeDirectory);
        var staging = Path.Combine(storeDirectory, ".staging");

        var packages = new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase);

        foreach (var platform in opts.Platforms)
        {
            ct.ThrowIfCancellationRequested();

            var payloadRoot = Path.Combine(staging, platform);
            var exec = LayOutFakeGame(payloadRoot, platform, opts);

            var archiveName = ArchiveNameFor(platform, opts.Version);
            var archivePath = Path.Combine(storeDirectory, archiveName);

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (UsesZip(platform))
            {
                ZipFile.CreateFromDirectory(payloadRoot, archivePath, CompressionLevel.Fastest, includeBaseDirectory: false);
            }
            else
            {
                await CreateTarGz(payloadRoot, archivePath, ct).ConfigureAwait(false);
            }

            packages[platform] = new PlatformPackage
            {
                // Relative on purpose: the manifest stays valid wherever the folder is moved.
                Url = archiveName,
                Sha256 = await Sha256Verifier.ComputeAsync(archivePath, null, ct).ConfigureAwait(false),
                Size = new FileInfo(archivePath).Length,
                Exec = exec,
                Format = UsesZip(platform) ? ArchiveFormat.Zip : ArchiveFormat.TarGz,
            };
        }

        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        var manifest = new ReleaseManifest
        {
            Version = opts.Version,
            Released = DateTimeOffset.UtcNow,
            Notes = opts.Notes,
            Platforms = packages,
        };

        var manifestPath = Path.Combine(storeDirectory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, ManifestJson.Write(manifest), ct).ConfigureAwait(false);

        return manifestPath;
    }

    private static bool UsesZip(string platform) => platform == PlatformKey.WindowsX64;

    private static string ArchiveNameFor(string platform, string version) =>
        UsesZip(platform)
            ? $"FriWorld-{version}-{platform}.zip"
            : $"FriWorld-{version}-{platform}.tar.gz";

    /// <summary>
    /// Writes a stand-in for a game build and returns the exec path relative to the install root.
    /// The macOS layout uses a real bundle shape so the bundle-resolution path gets exercised.
    /// </summary>
    private static string LayOutFakeGame(string root, string platform, Options opts)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);

        string execRelative;
        string execAbsolute;

        if (platform == PlatformKey.WindowsX64)
        {
            execRelative = "FriWorld.cmd";
            execAbsolute = Path.Combine(root, execRelative);
            File.WriteAllText(execAbsolute, WindowsStub(opts));
        }
        else if (platform.StartsWith("osx", StringComparison.Ordinal))
        {
            var macOsDir = Path.Combine(root, "FriWorld.app", "Contents", "MacOS");
            Directory.CreateDirectory(macOsDir);
            Directory.CreateDirectory(Path.Combine(root, "FriWorld.app", "Contents", "Resources"));

            File.WriteAllText(Path.Combine(root, "FriWorld.app", "Contents", "Info.plist"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
                "<key>CFBundleExecutable</key><string>FriWorld</string></dict></plist>\n");

            execRelative = Path.Combine("FriWorld.app", "Contents", "MacOS", "FriWorld");
            execAbsolute = Path.Combine(root, execRelative);
            WriteShellStub(execAbsolute, opts);
        }
        else
        {
            execRelative = "FriWorld";
            execAbsolute = Path.Combine(root, execRelative);
            WriteShellStub(execAbsolute, opts);
        }

        MarkExecutable(execAbsolute);

        var dataDirectory = Path.Combine(root, "FriWorld_Data");
        Directory.CreateDirectory(dataDirectory);

        File.WriteAllText(Path.Combine(dataDirectory, "version.txt"), opts.Version + Environment.NewLine);
        WriteFiller(Path.Combine(dataDirectory, "payload.bin"), opts.PayloadBytes, opts.Version);

        TryCreateSymlink(Path.Combine(dataDirectory, "current-version.txt"), "version.txt");

        return execRelative;
    }

    /// <summary>
    /// A batch file that announces itself and then holds the console open.
    ///
    /// It waits with ping rather than timeout because timeout refuses to run when its input is
    /// not a console, and the launcher starts this through cmd without a terminal of its own.
    /// </summary>
    private static string WindowsStub(Options opts)
    {
        var lines = new List<string>
        {
            "@echo off",
            $"title FriWorld {opts.Version} (mock)",
            $"echo FriWorld mock build {opts.Version} started.",
            "echo.",
            "echo Toto nie je hra. Je to nahrada, aby sa launcher dal skusat",
            "echo bez cakania na skutocny build.",
        };

        if (opts.StubRunsForSeconds > 0)
        {
            lines.Add("echo.");
            lines.Add($"echo Okno sa samo zavrie o {opts.StubRunsForSeconds} sekund.");
            lines.Add($"ping -n {opts.StubRunsForSeconds + 1} 127.0.0.1 >nul");
        }

        return string.Join("\r\n", lines) + "\r\n";
    }

    private static void WriteShellStub(string path, Options opts)
    {
        var lines = new List<string>
        {
            "#!/bin/sh",
            $"echo \"FriWorld mock build {opts.Version} started.\"",
        };

        if (opts.StubRunsForSeconds > 0)
        {
            lines.Add($"sleep {opts.StubRunsForSeconds}");
        }

        // Written with unix line endings; a CRLF shebang line is a classic "bad interpreter" failure.
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
    }

    private static void MarkExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    /// <summary>Deterministic filler, so rebuilding the same version produces the same checksum.</summary>
    private static void WriteFiller(string path, int bytes, string seed)
    {
        var random = new Random(seed.GetHashCode(StringComparison.Ordinal));
        var buffer = new byte[64 * 1024];
        var remaining = bytes;

        using var stream = File.Create(path);

        while (remaining > 0)
        {
            random.NextBytes(buffer);
            var chunk = Math.Min(buffer.Length, remaining);
            stream.Write(buffer, 0, chunk);
            remaining -= chunk;
        }
    }

    private static void TryCreateSymlink(string linkPath, string target)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Windows needs Developer Mode for this. The symlink only exists to prove tar keeps it,
            // so a plain file is an acceptable substitute when the OS will not cooperate.
            File.WriteAllText(linkPath, target);
        }
    }

    private static async Task CreateTarGz(string sourceDirectory, string archivePath, CancellationToken ct)
    {
        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        await TarFile
            .CreateFromDirectoryAsync(sourceDirectory, gzip, includeBaseDirectory: false, ct)
            .ConfigureAwait(false);
    }
}
