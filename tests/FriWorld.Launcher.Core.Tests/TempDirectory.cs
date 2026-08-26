namespace FriWorld.Launcher.Core.Tests;

/// <summary>A scratch directory that removes itself when the test finishes.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(string label)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "friworld-launcher-tests",
            $"{label}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked handle should not fail an otherwise passing test.
        }
    }
}
