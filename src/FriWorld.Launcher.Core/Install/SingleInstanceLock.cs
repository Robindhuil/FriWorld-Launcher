using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Install;

/// <summary>
/// An exclusive lock on the install tree, held for as long as the launcher runs.
///
/// Two launchers downloading into the same <c>game.new</c> at once would interleave writes and
/// produce an install that passes no check and fails in a confusing way, so the second one is
/// refused instead.
/// </summary>
public sealed class SingleInstanceLock : IDisposable
{
    private readonly FileStream _stream;

    private SingleInstanceLock(FileStream stream) => _stream = stream;

    /// <summary>
    /// Returns null when another launcher already holds the lock.
    ///
    /// Retries briefly rather than refusing immediately. A launcher that has just replaced itself
    /// starts its successor and then exits, and the operating system does not always release the
    /// handle in that order — without a moment's patience the new launcher would refuse to start
    /// and the update would look like it had broken everything.
    /// </summary>
    public static SingleInstanceLock? TryAcquire(LauncherPaths paths, TimeSpan? waitFor = null)
    {
        var deadline = DateTime.UtcNow + (waitFor ?? TimeSpan.FromSeconds(3));

        while (true)
        {
            if (TryOnce(paths) is { } acquired)
            {
                return acquired;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            Thread.Sleep(100);
        }
    }

    private static SingleInstanceLock? TryOnce(LauncherPaths paths)
    {
        Directory.CreateDirectory(paths.Root);

        try
        {
            var stream = new FileStream(
                paths.LockFile,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);

            // Recording the pid makes a stale lock diagnosable rather than mysterious.
            var pid = System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
            stream.Write(pid);
            stream.Flush();

            return new SingleInstanceLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
