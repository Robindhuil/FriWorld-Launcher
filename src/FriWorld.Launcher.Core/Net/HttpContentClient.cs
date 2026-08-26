using System.Net;
using System.Net.Http.Headers;

namespace FriWorld.Launcher.Core.Net;

/// <summary>
/// Downloads over HTTP with resume support.
///
/// Two things here are not obvious. First, a partial file is always re-requested from the
/// canonical URL rather than from whatever redirect target was resolved last time: release hosts
/// hand out short-lived signed URLs, and reusing one across a multi-gigabyte download means the
/// resume attempt fails once the signature expires. Second, a server is free to ignore a Range
/// header and answer 200 with the whole body, so the partial file is overwritten in that case
/// instead of being appended to, which would silently produce a corrupt archive.
/// </summary>
public sealed class HttpContentClient : IContentClient, IDisposable
{
    private const int BufferSize = 1024 * 1024;
    private const int MaxAttempts = 4;

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public HttpContentClient(HttpClient? http = null)
    {
        _ownsClient = http is null;
        _http = http ?? CreateDefaultClient();
    }

    public static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        var client = new HttpClient(handler)
        {
            // A per-request timeout would kill a long download, so the body read is bounded by
            // cancellation instead and only the header exchange is time-limited, per call.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        // GitHub rejects requests without a User-Agent outright.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FriWorld-Launcher/0.1");
        return client;
    }

    public bool CanHandle(Uri uri) => uri.Scheme is "http" or "https";

    public async Task<string> GetStringAsync(Uri uri, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var response = await _http.GetAsync(uri, timeout.Token).ConfigureAwait(false);
        await ThrowIfUnsuccessful(response, uri).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
    }

    public async Task DownloadToFileAsync(
        Uri uri,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var partPath = destinationPath + ".part";

        DiscardUnusablePartial(partPath, expectedSize);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadAttempt(uri, partPath, expectedSize, progress, ct).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex) && !ct.IsCancellationRequested)
            {
                // Back off, then resume from whatever landed on disk.
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(partPath, destinationPath);
    }

    private async Task DownloadAttempt(
        Uri uri,
        string partPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var already = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        if (expectedSize is > 0 && already == expectedSize)
        {
            progress?.Report(new DownloadProgress(already, expectedSize, 0));
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (already > 0)
        {
            request.Headers.Range = new RangeHeaderValue(already, null);
        }

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        // 416 means the partial is at or past the end of the resource, so the local file is stale.
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partPath);
            throw new HttpRequestException("Partial download no longer matches the source; restarting.");
        }

        await ThrowIfUnsuccessful(response, uri).ConfigureAwait(false);

        var resuming = response.StatusCode == HttpStatusCode.PartialContent;

        // When a Range request comes back as a plain 200 the server ignored it and is sending the
        // whole body, so anything already on disk has to be thrown away rather than appended to.
        var startOffset = resuming ? already : 0;

        var total = expectedSize
            ?? (resuming
                ? response.Content.Headers.ContentRange?.Length
                : response.Content.Headers.ContentLength);

        var reporter = new ThrottledProgress(progress, total);

        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = new FileStream(
            partPath,
            startOffset > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            useAsync: true);

        var buffer = new byte[BufferSize];
        var written = startOffset;

        while (true)
        {
            var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            written += read;
            reporter.Report(written);
        }

        reporter.ReportFinal(written);

        if (expectedSize is > 0 && written != expectedSize)
        {
            throw new HttpRequestException(
                $"Download ended at {written} bytes, the manifest says {expectedSize}.");
        }
    }

    /// <summary>Throws away a partial file that cannot possibly grow into the expected one.</summary>
    private static void DiscardUnusablePartial(string partPath, long? expectedSize)
    {
        if (!File.Exists(partPath) || expectedSize is not > 0)
        {
            return;
        }

        if (new FileInfo(partPath).Length > expectedSize)
        {
            File.Delete(partPath);
        }
    }

    private static async Task ThrowIfUnsuccessful(HttpResponseMessage response, Uri uri)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var hint = response.StatusCode switch
        {
            HttpStatusCode.Forbidden when response.Headers.Contains("x-ratelimit-remaining")
                => " (rate limited — serve the manifest from storage that does not rate-limit)",
            HttpStatusCode.NotFound => " (no such release asset)",
            _ => string.Empty,
        };

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var excerpt = body.Length > 200 ? body[..200] : body;

        throw new HttpRequestException(
            $"GET {uri} returned {(int)response.StatusCode} {response.ReasonPhrase}{hint}. {excerpt}");
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException or TaskCanceledException;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
