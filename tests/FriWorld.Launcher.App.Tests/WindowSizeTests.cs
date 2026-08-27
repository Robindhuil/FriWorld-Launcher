using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// The arithmetic lives in <see cref="WindowFit"/> and is tested there. What these check is that
/// the window is actually wired to it — that the size and the content scale come from the same
/// factor, and that neither was left behind at a hard-coded 980 × 720.
/// </summary>
public class WindowSizeTests
{
    private static MainWindow Open()
    {
        WindowSandbox.FreshInstallRoot();

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static double ExpectedScale(MainWindow window)
    {
        var screen = window.Screens.Primary ?? window.Screens.All.First();
        var scaling = screen.Scaling;

        return WindowFit.ScaleFor(
            screen.WorkingArea.Width / scaling,
            screen.WorkingArea.Height / scaling);
    }

    [AvaloniaFact]
    public void The_window_is_sized_for_the_screen()
    {
        var window = Open();
        var expected = ExpectedScale(window);

        Assert.Equal(WindowFit.DesignWidth * expected, window.Width, 3);
        Assert.Equal(WindowFit.DesignHeight * expected, window.Height, 3);
    }

    [AvaloniaFact]
    public void The_contents_are_scaled_by_the_same_factor()
    {
        // Sizing the frame without scaling what is inside it would leave a 60px wordmark above a
        // squeezed action bar, which is worse than either.
        var window = Open();
        var expected = ExpectedScale(window);

        // By name, not by type: LayoutTransformControl's own template contains another one.
        var scaler = Named<LayoutTransformControl>(window, "Scaler");
        var transform = Assert.IsType<ScaleTransform>(scaler.LayoutTransform);

        Assert.Equal(expected, transform.ScaleX, 3);
        Assert.Equal(expected, transform.ScaleY, 3);
    }

    [AvaloniaFact]
    public void The_design_size_is_still_what_the_contents_are_laid_out_at()
    {
        // Everything inside is positioned in design units. If this border ever stops being
        // 980 x 720, every size in the XAML quietly starts meaning something else.
        var window = Open();

        var root = Named<Border>(window, "DesignRoot");

        Assert.Equal(WindowFit.DesignWidth, root.Width);
        Assert.Equal(WindowFit.DesignHeight, root.Height);
    }

    private static T Named<T>(MainWindow window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
}
