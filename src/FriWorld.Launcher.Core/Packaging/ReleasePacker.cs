using FriWorld.Launcher.Core.Manifest;
using FriWorld.Launcher.Core.Platform;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Packaging;

/// <summary>
/// Turns a folder of Unity player builds into a release: one archive per platform, checksums,
/// and the manifest.
///
/// Deliberately on this side of the fence rather than in the game's build script. The manifest is
/// the contract between the two repositories, and a contract with two independent implementations
/// drifts. Here the code that writes it and the code that reads it cannot disagree.
/// </summary>
public static class ReleasePacker
{
    public sealed record Options
    {
        /// <summary>Folder holding one subfolder per platform key, as produced by the Unity build.</summary>
        public required string InputDirectory { get; init; }

        public required string OutputDirectory { get; init; }

        /// <summary>The game's <c>bundleVersion</c>. Stamped into the manifest and archive names.</summary>
        public required string Version { get; init; }

        public string? Notes { get; init; }

        /// <summary>
        /// Prefix for the archive urls in the manifest. Leave null to write bare file names, which
        /// keeps the manifest valid wherever the folder is uploaded.
        /// </summary>
        public string? BaseUrl { get; init; }

        /// <summary>Overrides for the executable path, per platform key, when detection is wrong.</summary>
        public IReadOnlyDictionary<string, string> ExecOverrides { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Optional pointer at the newest launcher, for the update notice.</summary>
        public LauncherRelease? Launcher { get; init; }
    }

    public sealed record Result(string ManifestPath, IReadOnlyList<PackedPlatform> Platforms);

    public sealed record PackedPlatform(string PlatformKey, string ArchivePath, long Size, string Exec);

    public static async Task<Result> PackAsync(
        Options options,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        var input = Path.GetFullPath(options.InputDirectory);

        if (!Directory.Exists(input))
        {
            throw new PackagingException($"No such input directory: {input}");
        }

        var playerDirectories = Directory
            .EnumerateDirectories(input)
            .Where(d => IsKnownPlatform(Path.GetFileName(d)))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (playerDirectories.Count == 0)
        {
            throw new PackagingException(
                $"{input} has no platform subfolders. Expected one or more of: " +
                $"{PlatformKey.WindowsX64}, {PlatformKey.LinuxX64}, {PlatformKey.MacArm64}, {PlatformKey.MacX64}.");
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var packages = new Dictionary<string, PlatformPackage>(StringComparer.OrdinalIgnoreCase);
        var packed = new List<PackedPlatform>();

        foreach (var playerDirectory in playerDirectories)
        {
            ct.ThrowIfCancellationRequested();

            var platformKey = Path.GetFileName(playerDirectory);
            var format = FormatFor(platformKey);

            var exec = options.ExecOverrides.TryGetValue(platformKey, out var overridden)
                ? overridden
                : ExecutableFinder.Find(playerDirectory, platformKey);

            var archiveName = ArchiveName(options.Version, platformKey, format);
            var archivePath = Path.Combine(options.OutputDirectory, archiveName);

            log?.Report($"{platformKey}: packing {exec} into {archiveName}");

            // Signing, when there is ever a certificate, belongs here — after the build and
            // before the archive, because signing changes the file and every checksum below
            // is taken from what the archive actually contains.

            await ArchiveBuilder
                .CreateAsync(playerDirectory, archivePath, format, exec, ct)
                .ConfigureAwait(false);

            var sha = await Sha256Verifier.ComputeAsync(archivePath, null, ct).ConfigureAwait(false);
            var size = new FileInfo(archivePath).Length;

            packages[platformKey] = new PlatformPackage
            {
                Url = options.BaseUrl is null ? archiveName : CombineUrl(options.BaseUrl, archiveName),
                Sha256 = sha,
                Size = size,
                Exec = exec,
                Format = format,
            };

            packed.Add(new PackedPlatform(platformKey, archivePath, size, exec));
            log?.Report($"{platformKey}: {size:N0} bytes, sha256 {sha[..16]}…");
        }

        var manifest = new ReleaseManifest
        {
            Version = options.Version,
            Released = DateTimeOffset.UtcNow,
            Notes = options.Notes,
            Platforms = packages,
            Launcher = options.Launcher,
        };

        // Round-trip the manifest before writing it, so a release cannot ship a file the
        // launcher would refuse to parse.
        manifest.Validate();

        var manifestPath = Path.Combine(options.OutputDirectory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, ManifestJson.Write(manifest), ct).ConfigureAwait(false);
        ManifestJson.Parse(await File.ReadAllTextAsync(manifestPath, ct).ConfigureAwait(false));

        return new Result(manifestPath, packed);
    }

    private static bool IsKnownPlatform(string name) => name is
        PlatformKey.WindowsX64 or PlatformKey.LinuxX64 or PlatformKey.MacArm64 or PlatformKey.MacX64;

    /// <summary>
    /// Windows gets zip, everything else gets tar.gz. Not a style choice: zip cannot carry the
    /// unix execute bit and turns the symlinks inside a macOS bundle into ordinary files.
    /// </summary>
    private static ArchiveFormat FormatFor(string platformKey) =>
        platformKey == PlatformKey.WindowsX64 ? ArchiveFormat.Zip : ArchiveFormat.TarGz;

    private static string ArchiveName(string version, string platformKey, ArchiveFormat format) =>
        $"FriWorld-{version}-{platformKey}{(format == ArchiveFormat.Zip ? ".zip" : ".tar.gz")}";

    private static string CombineUrl(string baseUrl, string fileName) =>
        baseUrl.EndsWith('/') ? baseUrl + fileName : $"{baseUrl}/{fileName}";
}
