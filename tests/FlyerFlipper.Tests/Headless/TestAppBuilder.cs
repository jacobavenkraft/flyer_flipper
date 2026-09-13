using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(FlyerFlipper.Tests.Headless.TestAppBuilder))]

namespace FlyerFlipper.Tests.Headless;

/// <summary>
/// Avalonia headless platform for view-level tests (<c>[AvaloniaFact]</c>). Plain <c>[Fact]</c> tests are unaffected.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
