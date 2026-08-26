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
/// </summary>
public sealed class FileLauncherLog(string path, Action<string>? mirror = null) : ILauncherLog
{
    private readonly Lock _gate = new();

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
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // A launcher that cannot write its log still has to be able to launch the game.
            }
        }
    }
}
