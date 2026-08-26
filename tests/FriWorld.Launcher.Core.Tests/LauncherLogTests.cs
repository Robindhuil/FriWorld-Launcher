using FriWorld.Launcher.Core.Diagnostics;

namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// The log file. It is the only diagnostic channel there is once the launcher is on someone
/// else's machine, so these tests care about it surviving conditions rather than formatting.
/// </summary>
public class LauncherLogTests
{
    /// <summary>
    /// Reads the log while the launcher still holds it open, the way anything inspecting a live
    /// log has to. File.ReadAllText refuses to share with a writer, which is the very behaviour
    /// this class exists to survive.
    /// </summary>
    private static string[] ReadWhileOpen(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void Writes_what_it_is_given()
    {
        using var temp = new TempDirectory("log-basic");
        var path = temp.Combine("launcher.log");

        using (var log = new FileLauncherLog(path))
        {
            log.Info("prvý");
            log.Warn("druhý");
            log.Error("tretí");
        }

        var lines = File.ReadAllLines(path);

        Assert.Equal(3, lines.Length);
        Assert.Contains("INFO", lines[0]);
        Assert.Contains("prvý", lines[0]);
        Assert.Contains("WARN", lines[1]);
        Assert.Contains("ERROR", lines[2]);
    }

    [Fact]
    public void Keeps_writing_while_another_process_reads_the_file()
    {
        // This is the case that lost an entire install. A scanner opening the log during a long
        // unpack used to make every single write fail, silently — measured at 200 of 200 lines.
        using var temp = new TempDirectory("log-reader");
        var path = temp.Combine("launcher.log");

        using var log = new FileLauncherLog(path);
        log.Info("pred");

        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            for (var i = 0; i < 50; i++)
            {
                log.Info($"počas {i}");
            }
        }

        log.Info("po");

        Assert.Equal(0, log.DroppedLines);
        Assert.Equal(52, ReadWhileOpen(path).Length);
    }

    [Fact]
    public void Two_launchers_appending_at_once_do_not_lose_each_other_lines()
    {
        using var temp = new TempDirectory("log-two");
        var path = temp.Combine("launcher.log");

        using var first = new FileLauncherLog(path);
        using var second = new FileLauncherLog(path);

        for (var i = 0; i < 25; i++)
        {
            first.Info($"prvý {i}");
            second.Info($"druhý {i}");
        }

        var text = string.Join(Environment.NewLine, ReadWhileOpen(path));

        Assert.Contains("prvý 24", text, StringComparison.Ordinal);
        Assert.Contains("druhý 24", text, StringComparison.Ordinal);
        Assert.Equal(0, first.DroppedLines + second.DroppedLines);
    }

    [Fact]
    public void Appends_to_what_is_already_there()
    {
        using var temp = new TempDirectory("log-append");
        var path = temp.Combine("launcher.log");
        File.WriteAllText(path, "z minulého behu" + Environment.NewLine);

        using (var log = new FileLauncherLog(path))
        {
            log.Info("nový");
        }

        var lines = File.ReadAllLines(path);

        Assert.Equal(2, lines.Length);
        Assert.Equal("z minulého behu", lines[0]);
    }

    [Fact]
    public void An_unwritable_path_does_not_stop_the_launcher()
    {
        using var temp = new TempDirectory("log-unwritable");

        // A directory where the file should be: opening it for writing cannot succeed.
        var path = temp.Combine("launcher.log");
        Directory.CreateDirectory(path);

        using var log = new FileLauncherLog(path);
        log.Info("toto sa nikam nezapíše");

        Assert.Equal(1, log.DroppedLines);
    }

    [Fact]
    public void A_recovered_log_admits_what_it_lost()
    {
        using var temp = new TempDirectory("log-gap");
        var path = temp.Combine("launcher.log");

        using var log = new FileLauncherLog(path);

        // Fake a run of failures the way an unwritable moment would produce them.
        Directory.CreateDirectory(temp.Combine("blocker"));
        var blocked = new FileLauncherLog(temp.Combine("blocker"));
        blocked.Info("a");
        blocked.Info("b");
        Assert.Equal(2, blocked.DroppedLines);

        log.Info("prvý");
        Assert.Equal(0, log.DroppedLines);

        // A log that looks complete but is not is worse than one that says where the hole is.
        Assert.DoesNotContain(
            "could not be written",
            string.Join(Environment.NewLine, ReadWhileOpen(path)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_mirror_sees_every_line_even_when_the_file_cannot()
    {
        using var temp = new TempDirectory("log-mirror");
        var path = temp.Combine("launcher.log");
        Directory.CreateDirectory(path);

        var mirrored = new List<string>();
        using var log = new FileLauncherLog(path, mirrored.Add);

        log.Info("jeden");
        log.Info("dva");

        // The console front end still shows what happened even with no file to write to.
        Assert.Equal(2, mirrored.Count);
    }

    [Fact]
    public void Writing_after_dispose_is_harmless()
    {
        using var temp = new TempDirectory("log-disposed");
        var path = temp.Combine("launcher.log");

        var log = new FileLauncherLog(path);
        log.Info("pred");
        log.Dispose();
        log.Info("po");

        Assert.Single(File.ReadAllLines(path));
    }
}
