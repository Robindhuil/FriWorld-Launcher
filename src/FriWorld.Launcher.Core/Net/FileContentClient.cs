namespace FriWorld.Launcher.Core.Net;

/// <summary>
/// Serves <c>file://</c> URLs. This is what stands in for remote object storage during development:
/// point the manifest at a local folder and the entire download-verify-extract-swap path runs for
/// real, with only the network absent.
/// </summary>
public sealed class FileContentClient : IContentClient
{
    private const int CopyBufferSize = 1024 * 1024;

    public bool CanHandle(Uri uri) => uri.IsFile;

    public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
        File.ReadAllTextAsync(uri.LocalPath, ct);

    public async Task DownloadToFileAsync(
        Uri uri,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var source = uri.LocalPath;

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Mock source file not found: {source}", source);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var total = new FileInfo(source).Length;
        var reporter = new ThrottledProgress(progress, total);

        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        await using var output = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);

        var buffer = new byte[CopyBufferSize];
        long copied = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            copied += read;
            reporter.Report(copied);
        }

        reporter.ReportFinal(copied);
    }
}
