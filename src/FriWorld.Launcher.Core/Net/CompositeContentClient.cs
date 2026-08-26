namespace FriWorld.Launcher.Core.Net;

/// <summary>Routes each URI to the first client that claims it.</summary>
public sealed class CompositeContentClient(params IContentClient[] clients) : IContentClient
{
    private readonly IReadOnlyList<IContentClient> _clients = clients;

    /// <summary>The default set: https/http for real releases, file for local mock storage.</summary>
    public static CompositeContentClient CreateDefault(HttpClient? http = null) =>
        new(new HttpContentClient(http), new FileContentClient());

    public bool CanHandle(Uri uri) => _clients.Any(c => c.CanHandle(uri));

    public Task<string> GetStringAsync(Uri uri, CancellationToken ct) =>
        Pick(uri).GetStringAsync(uri, ct);

    public Task DownloadToFileAsync(
        Uri uri,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct) =>
        Pick(uri).DownloadToFileAsync(uri, destinationPath, expectedSize, progress, ct);

    private IContentClient Pick(Uri uri) =>
        _clients.FirstOrDefault(c => c.CanHandle(uri))
        ?? throw new NotSupportedException($"No content client handles scheme '{uri.Scheme}'.");
}
