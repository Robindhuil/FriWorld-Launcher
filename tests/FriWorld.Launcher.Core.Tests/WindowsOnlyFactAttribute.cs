namespace FriWorld.Launcher.Core.Tests;

/// <summary>
/// A test that can only be staged on Windows, because the failure it provokes needs a mandatory
/// file lock. Elsewhere locks are advisory and the operation the test expects to fail succeeds.
///
/// This marks the <em>test technique</em> as Windows-only, never the behaviour. Skipping is
/// deliberate rather than an early <c>return</c>: a test that quietly passes without running is
/// worse than no test, because it reads as coverage.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Needs a mandatory file lock; locks are advisory outside Windows.";
        }
    }
}
