using Avalonia.Controls;
using Avalonia.Interactivity;
using FriWorld.Launcher.App.ViewModels;

namespace FriWorld.Launcher.App;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The check starts on its own: a launcher that needs to be told to look for updates is
        // just a shortcut with extra steps.
        await _viewModel.RefreshAsync();
    }
}
