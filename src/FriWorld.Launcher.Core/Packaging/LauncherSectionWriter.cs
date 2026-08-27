using System.Text.Json;
using System.Text.Json.Nodes;
using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Packaging;

/// <summary>
/// Rewrites only the <c>launcher</c> section of a manifest that already exists.
///
/// A launcher release does not touch the game, so regenerating the whole manifest with
/// <see cref="ReleasePacker"/> would need the game's build output — which is normally deleted
/// long before the next launcher goes out. Editing the file by hand works, but it throws away the
/// one guarantee packing has: that the file is read back through the launcher's own reader before
/// anyone sees it. This keeps that guarantee.
///
/// The edit is done on the JSON tree rather than by deserialising and re-serialising, so anything
/// the manifest carries that this version of the launcher does not know about survives. Tolerating
/// unknown fields is worth nothing if the tooling quietly drops them.
/// </summary>
public static class LauncherSectionWriter
{
    /// <summary>
    /// Replaces the <c>launcher</c> section of the manifest at <paramref name="manifestPath"/>,
    /// or removes it when <paramref name="launcher"/> is null.
    ///
    /// Removing it is the safe rollback: without the section a launcher only offers a download
    /// page, which is a working state rather than a broken one.
    /// </summary>
    public static void Write(string manifestPath, LauncherRelease? launcher)
    {
        var full = Path.GetFullPath(manifestPath);

        if (!File.Exists(full))
        {
            throw new PackagingException($"No manifest to edit at {full}.");
        }

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(File.ReadAllText(full));
        }
        catch (JsonException ex)
        {
            throw new PackagingException($"{full} is not valid JSON: {ex.Message}");
        }

        if (root is not JsonObject document)
        {
            throw new PackagingException($"{full} is not a JSON object.");
        }

        if (launcher is null)
        {
            document.Remove("launcher");
        }
        else
        {
            if (!launcher.IsUsable)
            {
                throw new PackagingException(
                    "The launcher section would be unusable: it needs a version and an absolute " +
                    "http or https download page.");
            }

            // A binary the launcher would refuse is worse than none at all: the release looks
            // finished, and self-update quietly degrades to a link nobody notices is a link.
            foreach (var (platform, binary) in launcher.Platforms)
            {
                if (!binary.IsUsable)
                {
                    throw new PackagingException(
                        $"The launcher binary for {platform} would be ignored. It needs a 64 " +
                        "character sha256, a size above zero, and an https url — the launcher " +
                        "replaces itself with this file.");
                }
            }

            document["launcher"] = JsonSerializer.SerializeToNode(launcher, ManifestJson.Options);
        }

        var written = document.ToJsonString(ManifestJson.Options) + Environment.NewLine;

        // Read it back the way the launcher will, before it is anywhere anyone can reach it.
        // A manifest that fails here would take every launcher in the wild down with it.
        ManifestJson.Parse(written);

        File.WriteAllText(full, written);
    }
}
