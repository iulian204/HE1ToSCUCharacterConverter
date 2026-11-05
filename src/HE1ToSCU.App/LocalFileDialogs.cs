using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace HE1ToSCU.App;

internal interface ILocalFileDialogs
{
    Task<string?> OpenFolderAsync(FolderPickerOpenOptions options);
    Task<string[]> OpenFilesAsync(FilePickerOpenOptions options, string? start);
    Task<string?> SaveFileAsync(FilePickerSaveOptions options);
}

internal sealed class LocalFileDialogs(Window owner) : ILocalFileDialogs
{
    public async Task<string?> OpenFolderAsync(FolderPickerOpenOptions options)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(options);
        try { return folders.Count == 0 ? null : LocalPath(folders[0]); }
        finally { foreach (var folder in folders) folder.Dispose(); }
    }

    public async Task<string[]> OpenFilesAsync(FilePickerOpenOptions options, string? start)
    {
        var provider = owner.StorageProvider;
        using var location = start == null ? null : await provider.TryGetFolderFromPathAsync(start);
        options.SuggestedStartLocation = location;
        var files = await provider.OpenFilePickerAsync(options);
        try { return files.Select(LocalPath).ToArray(); }
        finally { foreach (var file in files) file.Dispose(); }
    }

    public async Task<string?> SaveFileAsync(FilePickerSaveOptions options)
    {
        using var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        return file == null ? null : LocalPath(file);
    }

    private static string LocalPath(IStorageItem item) =>
        item.TryGetLocalPath() ?? throw new IOException("Select a local file or folder.");
}
