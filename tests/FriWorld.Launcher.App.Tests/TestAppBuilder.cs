using Avalonia;
using Avalonia.Headless;
using FriWorld.Launcher.App;

[assembly: AvaloniaTestApplication(typeof(FriWorld.Launcher.App.Tests.TestAppBuilder))]

namespace FriWorld.Launcher.App.Tests;

/// <summary>
/// Boots the real <see cref="App"/> against Avalonia's headless platform, so these tests drive the
/// same window a person does — same XAML, same styles, same focus and key routing.
///
/// This exists because the keyboard cannot be reasoned about from the source. Whether Enter
/// reaches the button that has focus or the default button is a property of how Avalonia routes
/// the event, not of anything visible in the view model, and guessing at it shipped a launcher
/// where Tab moved the focus and Enter ignored it.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
