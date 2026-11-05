using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HE1ToSCU.Core;

namespace HE1ToSCU.App;

public partial class MainWindow : Window
{
    private enum Appearance { Automatic, Light, Dark }

    private static readonly FilePickerFileType ModelFiles = new("HE1 model") { Patterns = ["*.model"] };
    private static readonly FilePickerFileType HavokFiles = new("Havok HKX or XML") { Patterns = ["*.hkx", "*.xml"] };
    private static readonly FilePickerFileType EditorFiles = new("Acorn editor executable") { Patterns = ["*.exe"] };
    private static readonly FilePickerFileType LogFiles = new("Text log") { Patterns = ["*.log"] };
    private static string DefaultSettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HE1ToSCU", "settings.json");

    private readonly string settingsPath;
    private readonly ILocalFileDialogs dialogs;
    private readonly Func<bool> allowsMica;
    private readonly Func<ConversionRequest, IProgress<ConversionProgress>, CancellationToken, Task<ConversionResult>> convertAsync;
    private readonly ObservableCollection<FileInfo> animationFiles = [];
    private readonly Transitions micaFade =
    [
        new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new SineEaseInOut() }
    ];
    private Dictionary<string, string> settings = [];
    private CancellationTokenSource? cancellation;
    private IPlatformSettings? platformSettings;
    private IDisposable? backdropWatcher;
    private bool micaRequested;
    private string? modelPath;
    private string? lastModelName;

    public SourceGame SelectedGame => GameSelector.SelectedIndex == 1 ? SourceGame.SonicUnleashed : SourceGame.SonicGenerations;
    public bool SelectedGenerateMouthL => SelectedGame == SourceGame.SonicUnleashed && GenerateMouthLeft.IsChecked == true;

    public MainWindow() : this(DefaultSettingsPath) { }

    internal MainWindow(string settingsPath, ILocalFileDialogs? dialogs = null,
        Func<ConversionRequest, IProgress<ConversionProgress>, CancellationToken, Task<ConversionResult>>? convert = null,
        Func<bool>? allowsMica = null)
    {
        this.settingsPath = settingsPath;
        this.dialogs = dialogs ?? new LocalFileDialogs(this);
        this.allowsMica = allowsMica ?? WindowsBackdropSettings.AllowsMica;
        convertAsync = convert ?? new ConversionService().ConvertAsync;
        InitializeComponent();
        Animations.ItemsSource = animationFiles;
        const string defaultEditor = @"D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe";
        EditorPath.Text = File.Exists(defaultEditor) ? defaultEditor : "";

        try
        {
            if (File.Exists(settingsPath))
            {
                settings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settingsPath)) ?? [];
                SourceFolder.Text = settings.GetValueOrDefault("source") ?? "";
                OutputPath.Text = settings.GetValueOrDefault("output") ?? "";
                EditorPath.Text = settings.GetValueOrDefault("editor") ?? EditorPath.Text;
                GameSelector.SelectedIndex = settings.GetValueOrDefault("game") == "SonicUnleashed" ? 1 : 0;
                GenerateMouthLeft.IsChecked = bool.TryParse(settings.GetValueOrDefault("mouthLeft"), out bool mouthLeft) && mouthLeft;
                if (Directory.Exists(SourceFolder.Text))
                    LoadFolder(SourceFolder.Text);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            Append("Settings could not be restored: " + e.Message);
        }

        AppearanceSelector.SelectedIndex = settings.GetValueOrDefault("theme") switch
        {
            nameof(Appearance.Light) => (int)Appearance.Light,
            nameof(Appearance.Dark) => (int)Appearance.Dark,
            _ => (int)Appearance.Automatic
        };
        UpdateMouthOption();
        Append("Ready. Select a model, its matching skeleton, and the animations to export.");
        Append("Binary HKX: Generations Havok 2010.2.0; Unleashed Havok 5.5.0. 32-bit skeletons and spline/interleaved animations. XML: skeletons/interleaved. Unsupported variants stop with a specific error.");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        platformSettings = this.GetPlatformSettings();
        if (platformSettings != null)
            platformSettings.ColorValuesChanged += SystemColorsChanged;
        if (TryGetPlatformHandle()?.HandleDescriptor == "HWND")
            backdropWatcher = WindowsBackdropSettings.Watch(() => UpdateBackdrop());
        UpdateBackdrop();

        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen != null)
        {
            // The work area is in physical pixels. Leave room for the title bar.
            Width = Math.Min(Width, Math.Max(MinWidth, screen.WorkingArea.Width / screen.Scaling - 16));
            Height = Math.Min(Height, Math.Max(MinHeight, screen.WorkingArea.Height / screen.Scaling - 44));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty && IsActive && IsVisible)
            UpdateBackdrop(animateFocus: true);
        else if (change.Property == IsActiveProperty || change.Property == ActualTransparencyLevelProperty)
            UpdateBackdropCover(animateFocus: change.Property == IsActiveProperty);
    }

    private void UpdateBackdropCover(bool animateFocus)
    {
        if (BackdropCover is not { } layer) return;

        // Fade only the neutral layer over Mica, leaving the controls and native backdrop intact.
        bool hasMica = micaRequested && ActualTransparencyLevel == WindowTransparencyLevel.Mica;
        bool animate = animateFocus && hasMica && OperatingSystem.IsWindows() &&
            SystemParametersInfo(0x1042 /* SPI_GETCLIENTAREAANIMATION */, 0, out bool enabled, 0) && enabled;
        layer.Transitions = animate ? micaFade : null;
        layer.Opacity = !hasMica || !IsActive ? 1 : 0;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);

    protected override void OnClosed(EventArgs e)
    {
        backdropWatcher?.Dispose();
        backdropWatcher = null;
        if (platformSettings != null)
            platformSettings.ColorValuesChanged -= SystemColorsChanged;
        platformSettings = null;
        base.OnClosed(e);
    }

    private void SystemColorsChanged(object? sender, PlatformColorValues _)
    {
        if (Dispatcher.UIThread.CheckAccess()) UpdateBackdrop();
        else Dispatcher.UIThread.Post(() =>
        {
            if (platformSettings != null) UpdateBackdrop();
        });
    }

    private void UpdateBackdrop(bool animateFocus = false)
    {
        bool allowMica = false;
        try
        {
            allowMica = platformSettings?.GetColorValues().ContrastPreference == ColorContrastPreference.NoPreference
                && allowsMica();
        }
        catch (Exception e) when (WindowsBackdropSettings.IsUnavailable(e)) { }

        if (micaRequested == allowMica && TransparencyLevelHint.Contains(WindowTransparencyLevel.Mica) == allowMica)
        {
            if (animateFocus) UpdateBackdropCover(animateFocus: true);
            return;
        }

        micaRequested = allowMica;
        // Cover before changing the native hint. Avalonia may report Transparent on non-redirection surfaces.
        if (!allowMica) UpdateBackdropCover(animateFocus: false);
        try
        {
            TransparencyLevelHint = allowMica ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None] : [WindowTransparencyLevel.None];
        }
        catch (Exception e) when (WindowsBackdropSettings.IsUnavailable(e))
        {
            micaRequested = false;
        }
        UpdateBackdropCover(animateFocus: false);
    }

    private void AppearanceChanged(object? sender, SelectionChangedEventArgs e)
    {
        var appearance = (Appearance)AppearanceSelector.SelectedIndex;
        RequestedThemeVariant = appearance switch
        {
            Appearance.Light => ThemeVariant.Light,
            Appearance.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        if (!IsVisible || AppearanceSelector.SelectedIndex < 0) return;

        settings["theme"] = appearance.ToString();
        try { SaveSettings(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Append("Appearance preference could not be saved: " + ex.Message);
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings));
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (cancellation == null) return;
        CancelConversion();
        e.Cancel = true;
        Append("Cancelling conversion; close again after it stops.");
    }

    private void GameChanged(object? sender, SelectionChangedEventArgs e) => UpdateMouthOption();

    private void UpdateMouthOption()
    {
        if (GenerateMouthLeft != null)
            GenerateMouthLeft.IsVisible = SelectedGame == SourceGame.SonicUnleashed;
    }

    private void Append(string message)
    {
        string text = Log.Text ?? "";
        if (text.Length > 2_000_000)
            text = text[^1_000_000..];
        Log.Text = $"{text}{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}";
        Dispatcher.UIThread.Post(() => Log.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.ScrollToEnd(),
            DispatcherPriority.Loaded);
    }

    private void SourceFolderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        try { LoadFolder(SourceFolder.Text?.Trim() ?? ""); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Append(ex.Message);
        }
    }

    private void LoadFolder(string folder)
    {
        var models = new DirectoryInfo(folder).EnumerateFiles("*.model", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        SourceFolder.Text = folder;
        Models.ItemsSource = models;
        Models.SelectedIndex = models.Length == 0 ? -1 : 0;
        Append($"Found {models.Length} models in {folder}.");
    }

    private async void BrowseSource(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        if (await PickFolderAsync("Select extracted HE1 asset folder") is { } folder)
            LoadFolder(folder);
    });

    private async void BrowseModel(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        if ((await PickFilesAsync("Select HE1 character model", ModelFiles)).FirstOrDefault() is not { } path) return;
        LoadFolder(Path.GetDirectoryName(path)!);
        Models.SelectedItem = ((FileInfo[])Models.ItemsSource!).FirstOrDefault(file =>
            file.FullName.Equals(path, StringComparison.OrdinalIgnoreCase));
    });

    private void ModelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Models.SelectedItem is not FileInfo file)
        {
            modelPath = null;
            return;
        }

        modelPath = file.FullName;
        string skeleton = Path.ChangeExtension(file.FullName, "skl.hkx");
        SkeletonPath.Text = File.Exists(skeleton) ? skeleton : "";
        animationFiles.Clear();

        string nextName = Path.GetFileNameWithoutExtension(file.Name);
        if (string.IsNullOrWhiteSpace(OutputPath.Text))
            OutputPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SCU Exports", nextName);
        else if (lastModelName != null && Path.GetFileName(OutputPath.Text) == lastModelName)
            OutputPath.Text = Path.Combine(Path.GetDirectoryName(OutputPath.Text)!, nextName);
        lastModelName = nextName;
    }

    private async void BrowseSkeleton(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        if ((await PickFilesAsync("Select matching skeleton HKX", HavokFiles)).FirstOrDefault() is { } path)
            SkeletonPath.Text = path;
    });

    private async void AddAnimations(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        string? start = Directory.Exists(SourceFolder.Text) ? SourceFolder.Text : null;
        foreach (string path in await PickFilesAsync("Select animation HKX files", HavokFiles, true, start))
        {
            if (!animationFiles.Any(file => file.FullName.Equals(path, StringComparison.OrdinalIgnoreCase)))
                animationFiles.Add(new FileInfo(path));
        }
    });

    private void RemoveAnimations(object? sender, RoutedEventArgs e)
    {
        foreach (var file in Animations.SelectedItems!.Cast<FileInfo>().ToArray())
            animationFiles.Remove(file);
    }

    private async void BrowseOutput(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        if (await PickFolderAsync("Select parent export folder (a new character subfolder will be created)") is { } folder)
            OutputPath.Text = Path.Combine(folder, modelPath == null ? "character" : Path.GetFileNameWithoutExtension(modelPath));
    });

    private async void BrowseEditor(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        if ((await PickFilesAsync("Select Acorn editor for native resource compilation", EditorFiles)).FirstOrDefault() is { } path)
            EditorPath.Text = path;
    });

    private async void SaveLog(object? sender, RoutedEventArgs e) => await TryPickerAsync(async () =>
    {
        var path = await dialogs.SaveFileAsync(new FilePickerSaveOptions
        {
            Title = "Save conversion log",
            SuggestedFileName = "HE1ToSCU.log",
            DefaultExtension = "log",
            FileTypeChoices = [LogFiles],
            ShowOverwritePrompt = true
        });
        if (path != null)
            await File.WriteAllTextAsync(path, Log.Text ?? "");
    });

    private Task<string?> PickFolderAsync(string title) =>
        dialogs.OpenFolderAsync(new FolderPickerOpenOptions { Title = title });

    private Task<string[]> PickFilesAsync(string title, FilePickerFileType filter, bool multiple = false, string? start = null) =>
        dialogs.OpenFilesAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = multiple,
            FileTypeFilter = [filter]
        }, start);

    private async Task TryPickerAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or System.Runtime.InteropServices.COMException)
        {
            Append(e.Message);
        }
    }

    private async void Convert(object? sender, RoutedEventArgs e)
    {
        if (cancellation != null) return;
        if (modelPath == null || string.IsNullOrWhiteSpace(OutputPath.Text))
        {
            Append("Choose a source model and a new export folder first.");
            return;
        }

        var request = new ConversionRequest(modelPath, SkeletonPath.Text?.Trim() ?? "", animationFiles.Select(file => file.FullName).ToArray(),
            OutputPath.Text.Trim(), EditorPath.Text?.Trim() ?? "", SelectedGame, SelectedGenerateMouthL);
        using var operation = new CancellationTokenSource();
        cancellation = operation;
        Inputs.IsEnabled = false;
        ConvertButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        Progress.Value = 0;

        try
        {
            settings["source"] = SourceFolder.Text ?? "";
            settings["output"] = OutputPath.Text;
            settings["editor"] = EditorPath.Text ?? "";
            settings["game"] = request.Game.ToString();
            settings["mouthLeft"] = (GenerateMouthLeft.IsChecked == true).ToString();
            SaveSettings();

            var progress = new Progress<ConversionProgress>(update =>
            {
                if (!ReferenceEquals(cancellation, operation)) return;
                Progress.Value = update.Percent;
                if (!operation.IsCancellationRequested) Status.Text = update.Message;
                Append(update.Message);
            });
            var result = await Task.Run(() => convertAsync(request, progress, operation.Token));
            Progress.Value = 100;
            Status.Text = $"Complete · {result.Vertices:N0} vertices · {result.Bones} bones · {result.Animations} animations";
            Append(result.BinaryValidated ? "Acorn loaded and re-saved the binary resources successfully." :
                "Exported text resources; engine validation was not requested.");
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Cancelled";
            Append("Conversion cancelled. Existing source and export files were preserved.");
        }
        catch (Exception ex)
        {
            Status.Text = "Conversion failed — see log";
            Append(ex.ToString());
        }
        finally
        {
            cancellation = null;
            Inputs.IsEnabled = true;
            ConvertButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private void Cancel(object? sender, RoutedEventArgs e) => CancelConversion();

    private void CancelConversion()
    {
        CancelButton.IsEnabled = false;
        cancellation?.Cancel();
        Status.Text = "Cancelling…";
    }
}
