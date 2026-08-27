namespace FriWorld.Launcher.Core.Platform;

/// <summary>
/// How large the launcher window should be on a given screen.
///
/// The window is designed once, at <see cref="DesignWidth"/> × <see cref="DesignHeight"/>, and
/// everything in it — type sizes, button heights, padding, the background render — is laid out in
/// those units. Fitting a smaller screen therefore means scaling the whole thing by one factor,
/// not resizing the frame around a fixed interior: a 60px wordmark above a squeezed action bar is
/// worse than a window that is simply smaller.
///
/// The fractions come from a 2103 × 1183 desktop where the design size was judged right. That is
/// 47% of its width and 61% of its height; the limits here sit a little above both, so that
/// screen keeps the full size even after a taskbar is taken off it, and anything smaller shrinks.
/// </summary>
public static class WindowFit
{
    public const double DesignWidth = 980;
    public const double DesignHeight = 720;

    /// <summary>At most half the width. Wider than this and the window stops reading as a window.</summary>
    private const double WidthFraction = 0.50;

    /// <summary>
    /// At most 65% of the height. Height is the binding constraint on almost every laptop, which
    /// is why this one is the looser of the two.
    /// </summary>
    private const double HeightFraction = 0.65;

    /// <summary>
    /// Below this the type stops being readable — the status line is 15px by design, and 70% of
    /// that is already 10.5px. A window slightly larger than the fractions allow is the lesser
    /// problem, and at this scale it still fits inside 1024 × 768.
    /// </summary>
    private const double MinimumScale = 0.70;

    /// <summary>
    /// Never larger than the design size. There is no more content to reveal, and the background
    /// is a raster render that would only soften.
    /// </summary>
    private const double MaximumScale = 1.0;

    /// <summary>
    /// The factor to apply to the whole window for a work area of the given size, in the same
    /// logical units as the window itself — screen pixels divided by the display's scaling.
    ///
    /// A work area that is missing or nonsensical yields the design size rather than a guess: an
    /// unknown screen is far more likely to be large enough than not.
    ///
    /// The result always fits inside the work area, even where that means going below
    /// <see cref="MinimumScale"/>.
    /// </summary>
    public static double ScaleFor(double workWidth, double workHeight)
    {
        if (double.IsNaN(workWidth) || double.IsNaN(workHeight) || workWidth <= 0 || workHeight <= 0)
        {
            return MaximumScale;
        }

        var byWidth = workWidth * WidthFraction / DesignWidth;
        var byHeight = workHeight * HeightFraction / DesignHeight;

        var preferred = Math.Clamp(Math.Min(byWidth, byHeight), MinimumScale, MaximumScale);

        // The floor can ask for more than the screen has. Readability loses that argument: a
        // window taller than the desktop cannot be dragged back by a title bar it does not have.
        var fitting = Math.Min(workWidth / DesignWidth, workHeight / DesignHeight);

        return Math.Min(preferred, Math.Min(fitting, MaximumScale));
    }

    /// <summary>The window size that goes with <see cref="ScaleFor"/>, rounded to whole units.</summary>
    public static (double Width, double Height) SizeFor(double workWidth, double workHeight)
    {
        var scale = ScaleFor(workWidth, workHeight);

        return (Math.Round(DesignWidth * scale), Math.Round(DesignHeight * scale));
    }
}
