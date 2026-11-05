using Avalonia.Platform.Storage;
using HE1ToSCU.App;

namespace HE1ToSCU.GuiTests;

internal sealed class TestPicker : ILocalFileDialogs
{
    public string? Folder { get; set; }
    public string[] Files { get; set; } = [];
    public string? SavePath { get; set; }
    public string? StartLocation { get; private set; }
    public FilePickerOpenOptions? OpenOptions { get; private set; }
    public FilePickerSaveOptions? SaveOptions { get; private set; }

    public Task<string[]> OpenFilesAsync(FilePickerOpenOptions options, string? start)
    {
        OpenOptions = options;
        StartLocation = start;
        return Task.FromResult(Files);
    }

    public Task<string?> OpenFolderAsync(FolderPickerOpenOptions options) => Task.FromResult(Folder);

    public Task<string?> SaveFileAsync(FilePickerSaveOptions options)
    {
        SaveOptions = options;
        return Task.FromResult(SavePath);
    }
}
