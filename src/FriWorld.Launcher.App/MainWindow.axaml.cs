using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Linq;
using FriWorld.Launcher.App.ViewModels;
using FriWorld.Launcher.Core.Platform;

namespace FriWorld.Launcher.App;

public partial class MainWindow : Window
{
    /// <summary>Where the pointer actually is inside the cursor artwork.</summary>
    private static readonly PixelPoint CursorHotspot = new(2, 2);

    private readonly LauncherViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        ApplyCustomCursor();
        FitToScreen();

        _viewModel.MinimiseRequested += (_, _) =>
            Dispatcher.UIThread.Post(() => WindowState = WindowState.Minimized);

        _viewModel.CloseRequested += (_, _) => Dispatcher.UIThread.Post(Close);

        // Hidden while the game has the screen, shown again when it exits. Hidden rather than
        // minimised: there is nothing to do in the launcher meanwhile, and a taskbar entry that
        // does nothing is one more thing between the person and the game.
        _viewModel.VisibilityRequested += (_, visible) => Dispatcher.UIThread.Post(() =>
        {
            if (visible)
            {
                Show();
                Activate();
            }
            else
            {
                Hide();
            }
        });
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The check starts on its own: a launcher that needs to be told to look for updates is
        // just a shortcut with extra steps.
        await _viewModel.RefreshAsync();
    }

    /// <summary>
    /// Drags the window by its background.
    ///
    /// With no system title bar there is nothing else to grab. Buttons handle their own presses,
    /// so this only ever sees clicks that landed on the chrome-free area.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    /// <summary>
    /// Sizes the window for the screen it is about to open on, and scales its contents by the
    /// same factor.
    ///
    /// Done before the window is shown, because a window that resizes itself once it is already
    /// on screen is a flicker people notice. That means <see cref="Screens.Primary"/> rather than
    /// the screen this window is on — the window has no position yet — which suits the centred
    /// startup position anyway.
    /// </summary>
    private void FitToScreen()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();

        // WorkingArea is in physical pixels and the window is measured in logical ones, so the
        // display scaling has to come out before the two can be compared.
        var scaling = screen?.Scaling ?? 1.0;

        var scale = screen is not null && scaling > 0
            ? WindowFit.ScaleFor(screen.WorkingArea.Width / scaling, screen.WorkingArea.Height / scaling)
            : 1.0;

        Scaler.LayoutTransform = new ScaleTransform(scale, scale);

        Width = WindowFit.DesignWidth * scale;
        Height = WindowFit.DesignHeight * scale;
    }

    private void ApplyCustomCursor()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://FriWorld.Launcher.App/Assets/cursor.png"));
            Cursor = new Cursor(new Bitmap(stream), CursorHotspot);
        }
        catch (Exception)
        {
            // A missing or unreadable cursor is not worth refusing to start over; the system
            // arrow is a perfectly good fallback.
        }
    }
}
