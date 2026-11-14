using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using HE1ToSCU.App;
using HE1ToSCU.Core;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HE1ToSCU.GuiTests.TestApplication))]

namespace HE1ToSCU.GuiTests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HE1ToSCU.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class GameSelectionTests : IDisposable
{
    private readonly DirectoryInfo temp = Directory.CreateTempSubdirectory("HE1ToSCU-GUI-");
    private string SettingsPath => Path.Combine(temp.FullName, "settings.json");

    [AvaloniaFact]
    public async Task DisabledPrrKeepsSavedPathButPassesTheOriginalConversionDefaults()
    {
        Touch(temp.FullName, "a.model");
        File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            source = temp.FullName, output = Path.Combine(temp.FullName, "export"), editor = "",
            exportPrr = "False", prrShaderPath = "res://kept-but-inactive"
        }));
        var started = new TaskCompletionSource<ConversionRequest>();
        var window = new MainWindow(SettingsPath, convert: (request, _, _) =>
        {
            started.SetResult(request);
            return Task.FromResult(new ConversionResult(request.OutputDirectory, 1, 3, 1, 0, false));
        });
        try
        {
            window.Show(); Click(window, "ConvertButton");
            var request = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(request.ExportPrrMaterials);
            Assert.Null(request.PrrShaderPath);
            Assert.Equal("res://kept-but-inactive", Control<TextBox>(window, "PrrShaderPath").Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PrrOptionsPersistWithoutConversionAndStayIndependentOfGameAndMouth()
    {
        var picker = new TestPicker();
        var window = new MainWindow(SettingsPath, picker);
        try
        {
            window.Show();
            var option = Control<CheckBox>(window, "ExportPrrMaterials");
            var path = Control<TextBox>(window, "PrrShaderPath");
            Assert.False(option.IsChecked == true);
            Assert.False(path.IsVisible);
            Assert.False(Control<Button>(window, "ShaderBrowseButton").IsVisible);
            option.IsChecked = true;
            Assert.True(path.IsVisible);
            path.Text = "res://PRR/Generations";
            Control<ComboBox>(window, "GameSelector").SelectedIndex = 1;
            Control<CheckBox>(window, "GenerateMouthLeft").IsChecked = true;
            Assert.True(option.IsChecked);
            Assert.Equal("res://PRR/Generations", path.Text);
            option.IsChecked = false;
            Assert.False(path.IsVisible);
            Assert.True(window.SelectedGenerateMouthL);
        }
        finally { window.Close(); }
        var reopened = new MainWindow(SettingsPath, picker);
        try
        {
            reopened.Show();
            Assert.False(Control<CheckBox>(reopened, "ExportPrrMaterials").IsChecked);
            Assert.Equal("res://PRR/Generations", Control<TextBox>(reopened, "PrrShaderPath").Text);
            Control<CheckBox>(reopened, "ExportPrrMaterials").IsChecked = true;
            Assert.True(Control<TextBox>(reopened, "PrrShaderPath").IsVisible);
            string project = temp.CreateSubdirectory("project").FullName;
            File.WriteAllText(Path.Combine(project, "project.godot"), "config_version=4");
            picker.Folder = Directory.CreateDirectory(Path.Combine(project, "shaders")).FullName;
            Click(reopened, "ShaderBrowseButton");
            Assert.Equal(picker.Folder, Control<TextBox>(reopened, "PrrShaderPath").Text);
        }
        finally { reopened.Close(); }
    }

    [AvaloniaFact]
    public void GameSelectionAndCompactFluentLayoutWorkInBothThemes()
    {
        var window = new MainWindow(SettingsPath) { Width = 560, Height = 420 };
        try
        {
            window.Show();
            var selector = Control<ComboBox>(window, "GameSelector");
            var mouth = Control<CheckBox>(window, "GenerateMouthLeft");
            Assert.Equal(SourceGame.SonicGenerations, window.SelectedGame);
            Assert.False(mouth.IsVisible);
            selector.SelectedIndex = 1;
            Assert.Equal(SourceGame.SonicUnleashed, window.SelectedGame);
            Assert.True(mouth.IsVisible);
            mouth.IsChecked = true;
            Assert.True(window.SelectedGenerateMouthL);
            selector.SelectedIndex = 0;
            Assert.False(mouth.IsVisible);
            Assert.False(window.SelectedGenerateMouthL);
            selector.SelectedIndex = 1;
            Assert.True(window.SelectedGenerateMouthL);

            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = theme;
                window.UpdateLayout();
                Assert.Equal(theme, window.ActualThemeVariant);
                var workspace = Control<ScrollViewer>(window, "WorkspaceScroll");
                Assert.True(workspace.Extent.Height > workspace.Viewport.Height,
                    $"Window {window.ClientSize}, scroll extent {workspace.Extent}, viewport {workspace.Viewport}");
                Assert.True(Control<Button>(window, "ConvertButton").Bounds.Width > 100);
                Assert.True(mouth.Bounds.Width > 110);
                var animations = Control<ListBox>(window, "Animations");
                Assert.Equal(new Thickness(1), animations.BorderThickness);
                Assert.Equal(new CornerRadius(4), animations.CornerRadius);
                Assert.Equal(Color.Parse(theme == ThemeVariant.Light ? "#EEEEEE" : "#1C1C1C"),
                    Assert.IsAssignableFrom<ISolidColorBrush>(animations.Background).Color);
            }

            Assert.True(selector.Focus());
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(mouth.IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AppearanceOverridesLiveThemeAndPreservesExistingSettingsAcrossRestarts()
    {
        File.WriteAllText(SettingsPath, """{"output":"kept-path","game":"SonicUnleashed","mouthLeft":"True"}""");
        var app = Application.Current!;
        var original = app.RequestedThemeVariant;
        var window = new MainWindow(SettingsPath);
        try
        {
            window.Show();
            var appearance = Control<ComboBox>(window, "AppearanceSelector");
            Assert.Equal(0, appearance.SelectedIndex);
            app.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            app.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);

            appearance.SelectedIndex = 1;
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            appearance.SelectedIndex = 2;
            app.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);

            var reopened = new MainWindow(SettingsPath);
            try
            {
                reopened.Show();
                Assert.Equal(2, Control<ComboBox>(reopened, "AppearanceSelector").SelectedIndex);
                Assert.Equal(ThemeVariant.Dark, reopened.ActualThemeVariant);
                Assert.Equal(SourceGame.SonicUnleashed, reopened.SelectedGame);
                Assert.True(reopened.SelectedGenerateMouthL);
                Assert.Equal("kept-path", Control<TextBox>(reopened, "OutputPath").Text);
                Control<ComboBox>(reopened, "AppearanceSelector").SelectedIndex = 0;
                Assert.Equal(ThemeVariant.Light, reopened.ActualThemeVariant);
                app.RequestedThemeVariant = ThemeVariant.Dark;
                Assert.Equal(ThemeVariant.Dark, reopened.ActualThemeVariant);
            }
            finally { reopened.Close(); }
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = original;
        }
    }

    [AvaloniaFact]
    public async Task PickersScanAndSelectAssetsAndSaveTheVisibleLog()
    {
        string source = temp.CreateSubdirectory("assets #1").FullName;
        string model = Touch(source, "Zulu.model");
        Touch(source, "Alpha.model");
        string skeleton = Touch(source, "Zulu.skl.hkx");
        string customSkeleton = Touch(source, "rig.xml");
        string first = Touch(source, "run.anm.hkx");
        string second = Touch(source, "idle.xml");
        string editor = Touch(source, "Acorn.exe");
        string log = Touch(temp.FullName, "export.log");

        var picker = new TestPicker();
        var window = new MainWindow(SettingsPath, picker);
        try
        {
            window.Show();
            var emptyState = Control<StackPanel>(window, "AnimationsEmptyState");
            var remove = Control<Button>(window, "AnimationRemoveButton");
            Assert.True(emptyState.IsVisible);
            Assert.False(remove.IsEnabled);
            picker.Folder = source;
            Click(window, "SourceBrowseButton");
            var models = Control<ComboBox>(window, "Models");
            Assert.Equal("Alpha.model", Assert.IsType<FileInfo>(models.SelectedItem).Name);

            picker.Files = [model];
            Click(window, "ModelBrowseButton");
            Assert.Equal(new[] { "*.model" }, picker.OpenOptions!.FileTypeFilter!.Single().Patterns);
            Assert.Equal(model, Assert.IsType<FileInfo>(models.SelectedItem).FullName);
            var skeletonField = Control<TextBox>(window, "SkeletonPath");
            Assert.Equal(skeleton, skeletonField.Text);

            picker.Files = [customSkeleton];
            Click(window, "SkeletonBrowseButton");
            Assert.Equal(new[] { "*.hkx", "*.xml" }, picker.OpenOptions!.FileTypeFilter!.Single().Patterns);
            Assert.Equal(customSkeleton, skeletonField.Text);

            picker.Files = [first, second, first];
            Click(window, "AnimationAddButton");
            Assert.True(picker.OpenOptions!.AllowMultiple);
            Assert.Equal(source, picker.StartLocation);
            var animations = Control<ListBox>(window, "Animations");
            Assert.Equal(2, animations.Items.Count);
            Assert.False(emptyState.IsVisible);
            Assert.False(remove.IsEnabled);
            animations.SelectedItems!.Add(animations.Items[0]);
            animations.SelectedItems.Add(animations.Items[1]);
            Assert.True(remove.IsEnabled);
            Click(window, "AnimationRemoveButton");
            Assert.Empty(animations.Items);
            Assert.True(emptyState.IsVisible);
            Assert.False(remove.IsEnabled);

            picker.Folder = temp.FullName;
            Click(window, "OutputBrowseButton");
            Assert.Equal(Path.Combine(temp.FullName, "Zulu"), Control<TextBox>(window, "OutputPath").Text);
            picker.Files = [editor];
            Click(window, "EditorBrowseButton");
            Assert.Equal(editor, Control<TextBox>(window, "EditorPath").Text);
            picker.Files = [];
            Click(window, "EditorBrowseButton");
            Assert.Equal(editor, Control<TextBox>(window, "EditorPath").Text);

            picker.SavePath = log;
            Click(window, "SaveLogButton");
            Assert.Equal("HE1ToSCU.log", picker.SaveOptions!.SuggestedFileName);
            Assert.True(picker.SaveOptions.ShowOverwritePrompt);
            await WaitFor(() => new FileInfo(log).Length > 0);
            Assert.Contains("Found 2 models", File.ReadAllText(log));

            picker.SavePath = temp.CreateSubdirectory("not-a-log-file").FullName;
            Click(window, "SaveLogButton");
            await WaitFor(() => Control<TextBox>(window, "Log").Text!.Contains(picker.SavePath));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task KeyboardScanAndConversionPassInputsReportProgressAndRestoreSettings()
    {
        string model = Touch(temp.FullName, "SonicRoot.model");
        string skeleton = Touch(temp.FullName, "SonicRoot.skl.hkx");
        string animation = Touch(temp.FullName, "idle.anm.hkx");
        string output = Path.Combine(temp.FullName, "export");
        var started = new TaskCompletionSource<ConversionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new TestPicker { Files = [animation] };
        var window = new MainWindow(SettingsPath, picker, async (request, progress, token) =>
        {
            progress.Report(new ConversionProgress(45, "Sampling animation"));
            started.SetResult(request);
            await finish.Task.WaitAsync(token);
            return new ConversionResult(output, 1, 3, 2, 1, false);
        });
        try
        {
            window.Show();
            Click(window, "ConvertButton");
            Assert.Contains("Choose a source model", Control<TextBox>(window, "Log").Text);
            var source = Control<TextBox>(window, "SourceFolder");
            source.Text = Path.Combine(temp.FullName, "missing");
            Assert.True(source.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.Contains(source.Text, Control<TextBox>(window, "Log").Text);
            source.Text = temp.FullName;
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Control<ComboBox>(window, "GameSelector").SelectedIndex = 1;
            Control<CheckBox>(window, "GenerateMouthLeft").IsChecked = true;
            Control<CheckBox>(window, "ExportPrrMaterials").IsChecked = true;
            Control<TextBox>(window, "PrrShaderPath").Text = "res://PRR/Unleashed";
            Control<TextBox>(window, "OutputPath").Text = output;
            Control<TextBox>(window, "EditorPath").Text = "";
            Click(window, "AnimationAddButton");
            Click(window, "ConvertButton");
            var request = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(model, request.ModelPath);
            Assert.Equal(skeleton, request.SkeletonPath);
            Assert.Equal([animation], request.AnimationPaths);
            Assert.Equal(output, request.OutputDirectory);
            Assert.Equal("", request.AcornEditorPath);
            Assert.Equal(SourceGame.SonicUnleashed, request.Game);
            Assert.True(request.GenerateMouthL);
            Assert.True(request.ExportPrrMaterials);
            Assert.Equal("res://PRR/Unleashed", request.PrrShaderPath);
            await WaitFor(() => Control<ProgressBar>(window, "Progress").Value == 45);
            Assert.Equal("Sampling animation", Control<TextBlock>(window, "Status").Text);
            Assert.False(Control<Grid>(window, "Inputs").IsEnabled);
            Assert.True(Control<Button>(window, "CancelButton").IsEnabled);
            finish.SetResult();
            await WaitFor(() => Control<Button>(window, "ConvertButton").IsEnabled);
            Assert.Equal(100, Control<ProgressBar>(window, "Progress").Value);
            Assert.StartsWith("Complete", Control<TextBlock>(window, "Status").Text);
        }
        finally { window.Close(); }

        var reopened = new MainWindow(SettingsPath);
        try
        {
            Assert.Equal(SourceGame.SonicUnleashed, reopened.SelectedGame);
            Assert.True(reopened.SelectedGenerateMouthL);
            Assert.Equal(output, Control<TextBox>(reopened, "OutputPath").Text);
            Assert.Equal(skeleton, Control<TextBox>(reopened, "SkeletonPath").Text);
            Assert.Equal("", Control<TextBox>(reopened, "EditorPath").Text);
            Assert.True(Control<CheckBox>(reopened, "ExportPrrMaterials").IsChecked);
            Assert.Equal("res://PRR/Unleashed", Control<TextBox>(reopened, "PrrShaderPath").Text);
        }
        finally { reopened.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelAndCloseKeepWindowOpenUntilConversionStops(bool close)
    {
        Touch(temp.FullName, "a.model");
        File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            source = temp.FullName,
            output = Path.Combine(temp.FullName, "export")
        }));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new MainWindow(SettingsPath, convert: async (_, _, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Expected cancellation.");
        });
        try
        {
            window.Show();
            Click(window, "ConvertButton");
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (close) window.Close();
            else Click(window, "CancelButton");
            Assert.True(window.IsVisible);
            await WaitFor(() => Control<Button>(window, "ConvertButton").IsEnabled);
            Assert.Equal("Cancelled", Control<TextBlock>(window, "Status").Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RealConversionRejectsExistingOutputAndSettingsWriteFailuresAreLogged()
    {
        Touch(temp.FullName, "a.model");
        string output = temp.CreateSubdirectory("export").FullName;
        string sentinel = Touch(output, "keep.txt");
        File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(new { source = temp.FullName, output, editor = "" }));
        var window = new MainWindow(SettingsPath);
        try
        {
            window.Show();
            Click(window, "ConvertButton");
            await WaitFor(() => Control<Button>(window, "ConvertButton").IsEnabled);
            Assert.StartsWith("Conversion failed", Control<TextBlock>(window, "Status").Text);
            Assert.Contains("already exists", Control<TextBox>(window, "Log").Text);
            Assert.Equal([sentinel], Directory.GetFiles(output));
        }
        finally { window.Close(); }

        var unwritable = new MainWindow(output);
        try
        {
            unwritable.Show();
            var field = Control<TextBox>(unwritable, "SourceFolder");
            field.Text = temp.FullName;
            Assert.True(field.Focus());
            unwritable.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Click(unwritable, "ConvertButton");
            await WaitFor(() => Control<Button>(unwritable, "ConvertButton").IsEnabled);
            Assert.StartsWith("Conversion failed", Control<TextBlock>(unwritable, "Status").Text);
            Assert.Contains(output, Control<TextBox>(unwritable, "Log").Text);
        }
        finally { unwritable.Close(); }
    }

    public void Dispose() => temp.Delete(recursive: true);

    private static string Touch(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "");
        return path;
    }

    private static T Control<T>(Window window, string name) where T : Control =>
        Assert.IsType<T>(window.FindControl<T>(name));

    private static void Click(Window window, string name)
    {
        var button = Control<Button>(window, name);
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
