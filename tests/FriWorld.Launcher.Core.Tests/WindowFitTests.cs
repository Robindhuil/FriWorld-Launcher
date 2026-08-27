using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.Core.Tests;

public class WindowFitTests
{
    [Fact]
    public void The_screen_it_was_designed_on_keeps_the_full_size()
    {
        // 2103 x 1183 is where the design size was judged right. The limits are set above what
        // that screen needs, so it still keeps the full size once a taskbar is taken off it.
        Assert.Equal(1.0, WindowFit.ScaleFor(2103, 1183));
        Assert.Equal(1.0, WindowFit.ScaleFor(2103, 1143));
    }

    [Fact]
    public void A_bigger_screen_does_not_get_a_bigger_window()
    {
        // There is no more content to reveal, and the background is a raster render.
        Assert.Equal(1.0, WindowFit.ScaleFor(3840, 2160));
    }

    [Theory]
    [InlineData(1920, 1032)]
    [InlineData(1680, 1002)]
    [InlineData(1600, 852)]
    public void A_smaller_screen_gets_a_smaller_window(double width, double height)
    {
        var scale = WindowFit.ScaleFor(width, height);

        Assert.True(scale < 1.0, $"scale was {scale}");
        Assert.True(scale >= 0.70, $"scale was {scale}");
    }

    [Theory]
    [InlineData(2103, 1183)]
    [InlineData(1920, 1032)]
    [InlineData(1600, 852)]
    [InlineData(1440, 852)]
    [InlineData(1366, 728)]
    [InlineData(1280, 672)]
    [InlineData(1024, 728)]
    [InlineData(800, 600)]
    [InlineData(640, 480)]
    public void The_window_always_fits_on_the_screen(double width, double height)
    {
        // The floor on the scale means the fractions can be exceeded on a very small screen. That
        // is deliberate — unreadable type is worse — but it must never push the window off the
        // edge, because there is no way to move a window nobody can reach the top of.
        var (windowWidth, windowHeight) = WindowFit.SizeFor(width, height);

        Assert.True(windowWidth <= width, $"{windowWidth} wide on a {width} screen");
        Assert.True(windowHeight <= height, $"{windowHeight} tall on a {height} screen");
    }

    [Fact]
    public void Height_is_what_binds_on_a_typical_laptop()
    {
        // Worth pinning down: a 16:9 laptop runs out of height long before width, so the height
        // fraction is the one that decides. Loosening the width limit would change nothing here.
        var scale = WindowFit.ScaleFor(1920, 1032);

        Assert.Equal(1032 * 0.65 / 720, scale, 6);
    }

    [Fact]
    public void The_aspect_ratio_never_changes()
    {
        // One factor for both axes. Two would stretch the render and the type with it.
        var (width, height) = WindowFit.SizeFor(1366, 728);

        Assert.Equal(
            WindowFit.DesignWidth / WindowFit.DesignHeight,
            width / height,
            2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 500)]
    [InlineData(double.NaN, 500)]
    public void An_unknown_screen_gets_the_design_size(double width, double height)
    {
        // Guessing small on a screen that failed to report itself would shrink the window for
        // everyone whose display driver is unusual. Large is the safer default.
        Assert.Equal(1.0, WindowFit.ScaleFor(width, height));
    }
}
