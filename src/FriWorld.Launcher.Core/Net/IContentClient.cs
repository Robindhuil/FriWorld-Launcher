namespace FriWorld.Launcher.Core.Net;

/// <summary>
/// Fetches bytes from somewhere. The whole point of this abstraction is that the rest of the
/// pipeline — verify, extract, swap, launch — has no idea whether the build came from GitHub,
/// from some other object storage later, or from a folder on this machine during development.
/// </summary>
public interface IContentClient
{
    bool CanHandle(Uri uri);

    Task<string> GetStringAsync(Uri uri, CancellationToken ct);

    /// <summary>
    /// Downloads to <paramref name="destinationPath"/>, resuming from a partial file when the
    /// source supports it. On return the file is complete; it is not verified here.
    /// </summary>
    Task DownloadToFileAsync(
        Uri uri,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct);
}
