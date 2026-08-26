using System.Text;

namespace FriWorld.Launcher.Core.Diagnostics;

public interface ILauncherLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}

/// <summary>Discards everything. Default when no log has been wired up.</summary>
public sealed class NullLauncherLog : ILauncherLog
{
    public static readonly NullLauncherLog Instance = new();

    public void Info(string message) { }

    public void Warn(string message) { }

    public void Error(string message, Exception? exception = null) { }
}

/// <summary>
/// Appends to <c>launcher.log</c> and optionally mirrors to a callback so the CLI can print the
/// same lines it writes. Deliberately tiny: a logging framework would be the largest dependency
/// in the project and would buy nothing a launcher needs.
///
/// The handle is opened once and kept, with <see cref="FileShare.ReadWrite"/>. That is not a
/// micro-optimisation. Opening and closing per line meant that any other process holding the file
/// — an antivirus scanner, the search indexer — made every write fail, and the failures were
/// swallowed. Measured: with a reader holding the file, 200 of 200 lines were lost. The gap
/// landed exactly where it hurts, over the minutes an install spends unpacking hundreds of
/// megabytes, which is when a scanner is most likely to be looking.
/// </summary>
public sealed class FileLauncherLog(string path, Action<string>? mirror = null) : ILauncherLog, IDisposable
{
    private readonly Lock _gate = new();

    private StreamWriter? _writer;
    private bool _disposed;
    private int _dropped;

    /// <summary>How many lines never reached the file. Zero unless something went wrong.</summary>
    public int DroppedLines
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    public void Info(string message) => Write("INFO ", message);

    public void Warn(string message) => Write("WARN ", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}";
        mirror?.Invoke(line);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var writer = Writer();

            if (writer is null)
            {
                _dropped++;
                return;
            }

            try
            {
                // A log that admits its own holes is worth more than one that looks complete.
                if (_dropped > 0)
                {
                    var lost = _dropped;
                    _dropped = 0;
                    writer.WriteLine(
                        $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} WARN  " +
                        $"{lost} log line(s) could not be written and are lost.");
                }

                writer.WriteLine(line);
            }
            catch (IOException)
            {
                // The handle went bad mid-write. Drop it so the next line reopens.
                _dropped++;
                Close();
            }
        }
    }

    /// <summary>
    /// The open writer, reopening if a previous attempt failed. Never throws — a launcher that
    /// cannot write its log still has to be able to install and start the game.
    /// </summary>
    private StreamWriter? Writer()
    {
        if (_writer is not null)
        {
            return _writer;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

            var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                // Readers must not be able to stop the launcher writing, and a second launcher
                // appending at the same time must not either.
                FileShare.ReadWrite);

            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            return _writer;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private void Close()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
            // Nothing useful to do while tearing down a handle that is already misbehaving.
        }

        _writer = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Close();
        }
    }
}
