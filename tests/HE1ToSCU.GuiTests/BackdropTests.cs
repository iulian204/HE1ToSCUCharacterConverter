using System.Runtime.InteropServices;
using System.Security;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using HE1ToSCU.App;
using Microsoft.Win32;
using Xunit;

namespace HE1ToSCU.GuiTests;

public sealed class BackdropTests
{
    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void MicaRequiresSupportedWindowsEnabledEffectsAndComposition(
        bool windowsSupported, bool transparency, bool composition, bool expected)
    {
        Assert.Equal(expected, WindowsBackdropSettings.Evaluate(windowsSupported, () => transparency, () => composition));
    }

    [Fact]
    public void UnsupportedWindowsSkipsNativeProbesAndApiFailuresFallBack()
    {
        static bool UnexpectedCall() => throw new InvalidOperationException("Unsupported Windows must skip the probes.");
        Assert.False(WindowsBackdropSettings.Evaluate(false, UnexpectedCall, UnexpectedCall));
        Assert.False(WindowsBackdropSettings.Evaluate(true, () => throw new SecurityException(), UnexpectedCall));
        Assert.False(WindowsBackdropSettings.Evaluate(true, () => true, () => throw new COMException()));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowCoversUnavailableMicaWithOpaqueBrushInBothThemes(bool requestMica)
    {
        using var settings = new TempSettings();
        var window = new MainWindow(settings.Path, allowsMica: () => requestMica);
        try
        {
            window.Show();
            Assert.Equal(requestMica, window.TransparencyLevelHint.Contains(WindowTransparencyLevel.Mica));
            Assert.NotEqual(WindowTransparencyLevel.Mica, window.ActualTransparencyLevel);
            var cover = Assert.IsType<Border>(window.FindControl<Border>("BackdropCover"));
            Assert.Equal(1, cover.Opacity);
            Assert.False(cover.IsHitTestVisible);
            window.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal(Color.Parse("#F3F3F3"), Assert.IsAssignableFrom<ISolidColorBrush>(cover.Background).Color);
            window.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(Color.Parse("#202020"), Assert.IsAssignableFrom<ISolidColorBrush>(cover.Background).Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FailedBackdropInitializationLeavesAnOpaqueWindow()
    {
        using var settings = new TempSettings();
        var window = new MainWindow(settings.Path, allowsMica: () => true);
        bool failed = false;
        window.PropertyChanged += (_, change) =>
        {
            if (change.Property != TopLevel.TransparencyLevelHintProperty) return;
            failed = true;
            throw new COMException("Simulated compositor failure.");
        };
        try
        {
            window.Show();
            Assert.True(failed);
            Assert.True(window.IsVisible);
            Assert.Equal(1, Assert.IsType<Border>(window.FindControl<Border>("BackdropCover")).Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task WindowsRegistryWatcherDeliversRepeatedNotifications()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Assert.Skip("This test exercises Windows 11 registry notifications.");
            return;
        }

        string path = @"Software\HE1ToSCU.Tests-" + Guid.NewGuid().ToString("N");
        using var key = Registry.CurrentUser.CreateSubKey(path);
        try
        {
            using var changed = new SemaphoreSlim(0);
            using var watcher = WindowsBackdropSettings.Watch(() => changed.Release(), path);
            Assert.NotNull(watcher);
            foreach (int value in new[] { 1, 0, 1 })
            {
                key.SetValue("EnableTransparency", value);
                Assert.True(await changed.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally { Registry.CurrentUser.DeleteSubKey(path); }
    }

    private sealed class TempSettings : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("HE1ToSCU-Backdrop-");
        public string Path => System.IO.Path.Combine(directory.FullName, "settings.json");
        public void Dispose() => directory.Delete();
    }
}
