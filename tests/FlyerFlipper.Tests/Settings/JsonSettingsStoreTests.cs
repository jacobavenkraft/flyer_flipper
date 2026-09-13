using System.Text.Json;
using FlyerFlipper.Core.Layout;
using FlyerFlipper.Core.Settings;
using FlyerFlipper.Core.Viewport;
using FlyerFlipper.Infrastructure.Settings;
using FlyerFlipper.Tests.TestSupport;

namespace FlyerFlipper.Tests.Settings;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string SettingsPath => Path.Combine(_temp.Path, "nested", "FlyerFlipper", "settings.json");

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static AppSettings FullySpecified() => new()
    {
        LastFolder = @"D:\flyers",
        Orientation = LayoutOrientation.Horizontal,
        ViewportMode = ViewportMode.Single,
        ViewedImageFileName = "Austin_TX.jpg",
        ScaleMode = ViewportScaleMode.ActualSize,
        ActiveProcessorTab = "Resize",
        Window = new WindowPlacement(-1200, 80, 1024.5, 700, IsMaximized: true),
        Processors = new Dictionary<string, JsonElement>
        {
            ["flyerflipper.grayscale"] = Json("""{"enabled":true}"""),
            ["flyerflipper.resize"] = Json("""{"enabled":true,"maxWidth":640,"maxHeight":480}"""),
        },
    };

    [Fact]
    public void Load_WhenNothingSaved_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Equal(AppSettings.CurrentVersion, settings.Version);
        Assert.Null(settings.LastFolder);
        Assert.Equal(LayoutOrientation.Vertical, settings.Orientation);
        Assert.Empty(settings.Processors);
        Assert.False(File.Exists(SettingsPath + JsonSettingsStore.BadFileSuffix));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEverything()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var saved = FullySpecified();

        store.Save(saved);
        var loaded = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(saved.LastFolder, loaded.LastFolder);
        Assert.Equal(saved.Orientation, loaded.Orientation);
        Assert.Equal(saved.ViewportMode, loaded.ViewportMode);
        Assert.Equal(saved.ViewedImageFileName, loaded.ViewedImageFileName);
        Assert.Equal(saved.ScaleMode, loaded.ScaleMode);
        Assert.Equal(saved.ActiveProcessorTab, loaded.ActiveProcessorTab);
        Assert.Equal(saved.Window, loaded.Window);
        Assert.Equal(["flyerflipper.grayscale", "flyerflipper.resize"], loaded.Processors.Keys.Order());
        Assert.Equal(640, loaded.Processors["flyerflipper.resize"].GetProperty("maxWidth").GetInt32());
    }

    [Fact]
    public void Save_WritesReadableJson_WithNamedEnumsAndEmbeddedProcessorObjects()
    {
        new JsonSettingsStore(SettingsPath).Save(FullySpecified());

        var text = File.ReadAllText(SettingsPath);

        Assert.Contains("\"orientation\": \"Horizontal\"", text);
        Assert.Contains("\"scaleMode\": \"ActualSize\"", text);
        Assert.Contains("\"flyerflipper.grayscale\": {", text); // an object, not an escaped string
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Save_ReplacesPreviousFile()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(FullySpecified());

        store.Save(new AppSettings { LastFolder = @"E:\other" });

        Assert.Equal(@"E:\other", store.Load().LastFolder);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"orientation":"Diagonal"}""")]
    [InlineData("[1, 2, 3]")]
    public void Load_CorruptFile_ReturnsDefaults_AndSetsFileAside(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, contents);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Null(settings.LastFolder);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(contents, File.ReadAllText(SettingsPath + JsonSettingsStore.BadFileSuffix));
    }

    [Fact]
    public void Load_CorruptFile_ReplacesAnOlderBadFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath + JsonSettingsStore.BadFileSuffix, "older");
        File.WriteAllText(SettingsPath, "newer garbage");

        new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal("newer garbage", File.ReadAllText(SettingsPath + JsonSettingsStore.BadFileSuffix));
    }

    [Fact]
    public void Load_UnreadableFile_ReturnsDefaultsWithoutThrowing()
    {
        new JsonSettingsStore(SettingsPath).Save(FullySpecified());
        using var exclusiveLock = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Null(settings.LastFolder);
    }

    [Fact]
    public void Load_IgnoresUnknownProperties_AndMissingOnesKeepDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """
            {
              // written by a newer version
              "version": 2,
              "lastFolder": "D:\\flyers",
              "someFutureSetting": { "x": 1 },
            }
            """);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(2, settings.Version);
        Assert.Equal(@"D:\flyers", settings.LastFolder);
        Assert.Equal(ViewportScaleMode.FitToWindow, settings.ScaleMode);
        Assert.Null(settings.Window);
    }

    [Fact]
    public void DefaultFilePath_IsPerUserFlyerFlipperSettingsJson()
    {
        var path = JsonSettingsStore.DefaultFilePath;

        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), path);
        Assert.EndsWith(Path.Combine("FlyerFlipper", "settings.json"), path);
    }
}
