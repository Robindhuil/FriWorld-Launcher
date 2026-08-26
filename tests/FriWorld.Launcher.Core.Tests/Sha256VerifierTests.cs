using System.Text;
using FriWorld.Launcher.Core.Verify;

namespace FriWorld.Launcher.Core.Tests;

public class Sha256VerifierTests
{
    // Known vector: SHA-256 of "abc".
    private const string AbcSha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public async Task Computes_a_known_checksum()
    {
        using var temp = new TempDirectory("sha");
        var file = temp.Combine("a.bin");
        await File.WriteAllBytesAsync(file, Encoding.ASCII.GetBytes("abc"));

        Assert.Equal(AbcSha, await Sha256Verifier.ComputeAsync(file));
    }

    [Fact]
    public async Task A_matching_file_passes_and_survives()
    {
        using var temp = new TempDirectory("sha-ok");
        var file = temp.Combine("a.bin");
        await File.WriteAllBytesAsync(file, Encoding.ASCII.GetBytes("abc"));

        await Sha256Verifier.VerifyOrDeleteAsync(file, AbcSha);

        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task A_mismatched_file_is_deleted_rather_than_left_to_be_extracted()
    {
        using var temp = new TempDirectory("sha-bad");
        var file = temp.Combine("a.bin");
        await File.WriteAllBytesAsync(file, Encoding.ASCII.GetBytes("not abc"));

        await Assert.ThrowsAsync<HashMismatchException>(
            () => Sha256Verifier.VerifyOrDeleteAsync(file, AbcSha));

        // Leaving it behind would let the next run skip the download and unpack a corrupt archive.
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Checksum_comparison_ignores_hex_casing()
    {
        using var temp = new TempDirectory("sha-case");
        var file = temp.Combine("a.bin");
        await File.WriteAllBytesAsync(file, Encoding.ASCII.GetBytes("abc"));

        await Sha256Verifier.VerifyOrDeleteAsync(file, AbcSha.ToUpperInvariant());

        Assert.True(File.Exists(file));
    }
}
