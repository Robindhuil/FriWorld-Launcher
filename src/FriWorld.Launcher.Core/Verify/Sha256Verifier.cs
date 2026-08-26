using System.Security.Cryptography;

namespace FriWorld.Launcher.Core.Verify;

/// <summary>
/// Hashes downloaded archives.
///
/// The rule the rest of the code relies on: an archive whose hash does not match the manifest is
/// deleted and the update fails. It is never extracted "just to see". A truncated download and a
/// tampered one look identical from here, and extracting either produces a broken install that is
/// far harder to diagnose than a failed check.
/// </summary>
public static class Sha256Verifier
{
    private const int BufferSize = 1024 * 1024;

    public static async Task<string> ComputeAsync(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var total = new FileInfo(filePath).Length;

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

        using var sha = SHA256.Create();
        var buffer = new byte[BufferSize];
        long read = 0;
        var lastReported = 0d;

        while (true)
        {
            var count = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            sha.TransformBlock(buffer, 0, count, null, 0);
            read += count;

            if (progress is not null && total > 0)
            {
                var fraction = (double)read / total;
                if (fraction - lastReported >= 0.01)
                {
                    lastReported = fraction;
                    progress.Report(fraction);
                }
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        progress?.Report(1d);

        return Convert.ToHexStringLower(sha.Hash!);
    }

    /// <summary>
    /// Checks the file against <paramref name="expectedSha256"/> and deletes it on mismatch, so a
    /// bad archive cannot be picked up by a later run that skips the download because a file exists.
    /// </summary>
    public static async Task VerifyOrDeleteAsync(
        string filePath,
        string expectedSha256,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var actual = await ComputeAsync(filePath, progress, ct).ConfigureAwait(false);

        if (string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDelete(filePath);

        throw new HashMismatchException(
            $"Checksum mismatch for {Path.GetFileName(filePath)}. " +
            $"Expected {expectedSha256}, got {actual}. The file has been deleted.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Nothing useful to do. The failed verification is the error worth reporting.
        }
    }
}

public sealed class HashMismatchException(string message) : Exception(message);
