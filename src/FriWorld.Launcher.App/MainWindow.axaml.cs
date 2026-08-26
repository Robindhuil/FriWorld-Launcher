using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FriWorld.Launcher.App.ViewModels;

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

        _viewModel.MinimiseRequested += (_, _) =>
            Dispatcher.UIThread.Post(() => WindowState = WindowState.Minimized);

        // The launcher's job ends when the game is running, so it gets out of the way. The view
        // model decides when that is; closing the window is the window's business.
        _viewModel.CloseRequested += (_, _) => Dispatcher.UIThread.Post(Close);
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
