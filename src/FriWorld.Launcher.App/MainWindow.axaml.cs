using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FriWorld.Launcher.App.ViewModels;

namespace FriWorld.Launcher.App;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

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
}
