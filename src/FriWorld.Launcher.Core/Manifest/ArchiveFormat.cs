namespace FriWorld.Launcher.Core.Manifest;

/// <summary>
/// The container an archive uses. This is not cosmetic: zip cannot carry the unix execute bit and
/// mangles the symlinks inside a macOS <c>.app</c> bundle, so those platforms must ship tar.gz.
/// </summary>
public enum ArchiveFormat
{
    Zip,
    TarGz,
}

public static class ArchiveFormats
{
    /// <summary>
    /// Guesses the format from a file name or URL. The manifest may state the format explicitly;
    /// this is the fallback so a fluid manifest does not have to carry the field.
    /// </summary>
    public static ArchiveFormat InferFrom(string urlOrFileName)
    {
        var path = urlOrFileName;

        // Strip query and fragment so a signed URL does not confuse the extension check.
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.TarGz;
        }

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveFormat.Zip;
        }

        throw new NotSupportedException(
            $"Cannot tell the archive format from '{urlOrFileName}'. Expected .zip, .tar.gz or .tgz.");
    }
}
