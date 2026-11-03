using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HE1ToSCU.Core;
using Microsoft.Win32;

namespace HE1ToSCU.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<FileInfo> animationFiles=[];
    private CancellationTokenSource? cancellation;
    private string? modelPath;
    private string? lastModelName;
    private static string SettingsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HE1ToSCU","settings.json");
    public MainWindow()
    {
        InitializeComponent();Animations.ItemsSource=animationFiles;
        EditorPath.Text=File.Exists(@"D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe")?@"D:\AcornDirFix\BSE_Editor_Acorn1.1.1.exe":"";
        try
        {
            if(File.Exists(SettingsPath))
            {
                var settings=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(SettingsPath));
                SourceFolder.Text=settings?.GetValueOrDefault("source")??"";OutputPath.Text=settings?.GetValueOrDefault("output")??"";EditorPath.Text=settings?.GetValueOrDefault("editor")??EditorPath.Text;
                if(Directory.Exists(SourceFolder.Text))LoadFolder(SourceFolder.Text);
            }
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){Append("Settings could not be restored: "+e.Message);}
        Append("Ready. Select a model, its matching skeleton, and the animations to export.");
        Append("Binary HKX: 32-bit Havok 2010.2.0 skeletons, spline/interleaved animations. XML: skeletons/interleaved. Unsupported variants stop with a specific error.");
        Closing+=(_,e)=>{if(cancellation!=null){cancellation.Cancel();e.Cancel=true;Append("Cancelling conversion; close again after it stops.");}};
    }
    private void Append(string message)
    {
        if(Log.Text.Length>2_000_000)Log.Text=Log.Text[^1_000_000..];
        Log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");Log.ScrollToEnd();
    }
    private void SourceFolderKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
    {
        if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;TryUi(()=>LoadFolder(SourceFolder.Text.Trim()));}
    }
    private void LoadFolder(string folder)
    {
        var models=new DirectoryInfo(folder).EnumerateFiles("*.model",SearchOption.TopDirectoryOnly).OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        SourceFolder.Text=folder;Models.ItemsSource=models;
        if(models.Length>0)Models.SelectedIndex=0;
        Append($"Found {models.Length} models in {folder}.");
    }
    private void BrowseSource(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFolderDialog{Title="Select extracted HE1 asset folder"};
        if(picker.ShowDialog(this)==true)TryUi(()=>LoadFolder(picker.FolderName));
    }
    private void BrowseModel(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Filter="HE1 model|*.model",Title="Select HE1 character model"};
        if(picker.ShowDialog(this)==true)TryUi(()=>{LoadFolder(Path.GetDirectoryName(picker.FileName)!);Models.SelectedItem=((FileInfo[])Models.ItemsSource).First(f=>f.FullName==picker.FileName);});
    }
    private void ModelChanged(object sender,SelectionChangedEventArgs e)
    {
        if(Models.SelectedItem is not FileInfo file){modelPath=null;return;}
        modelPath=file.FullName;string skeleton=Path.ChangeExtension(file.FullName,"skl.hkx");
        SkeletonPath.Text=File.Exists(skeleton)?skeleton:"";animationFiles.Clear();
        string nextName=Path.GetFileNameWithoutExtension(file.Name);
        if(string.IsNullOrWhiteSpace(OutputPath.Text))OutputPath.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SCU Exports",nextName);
        else if(lastModelName!=null&&Path.GetFileName(OutputPath.Text)==lastModelName)OutputPath.Text=Path.Combine(Path.GetDirectoryName(OutputPath.Text)!,nextName);
        lastModelName=nextName;
    }
    private void BrowseSkeleton(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Filter="Havok skeleton|*.hkx;*.xml",Title="Select matching skeleton HKX"};
        if(picker.ShowDialog(this)==true)SkeletonPath.Text=picker.FileName;
    }
    private void AddAnimations(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Filter="Havok animation|*.hkx;*.xml",Multiselect=true,Title="Select animation HKX files",InitialDirectory=Directory.Exists(SourceFolder.Text)?SourceFolder.Text:""};
        if(picker.ShowDialog(this)==true)foreach(string file in picker.FileNames)if(!animationFiles.Any(f=>f.FullName.Equals(file,StringComparison.OrdinalIgnoreCase)))animationFiles.Add(new FileInfo(file));
    }
    private void RemoveAnimations(object sender,RoutedEventArgs e){foreach(var file in Animations.SelectedItems.Cast<FileInfo>().ToArray())animationFiles.Remove(file);}
    private void BrowseOutput(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFolderDialog{Title="Select parent export folder (a new character subfolder will be created)"};
        if(picker.ShowDialog(this)==true)OutputPath.Text=Path.Combine(picker.FolderName,modelPath==null?"character":Path.GetFileNameWithoutExtension(modelPath));
    }
    private void BrowseEditor(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Filter="Acorn editor executable|*.exe",Title="Select Acorn editor for native resource compilation"};
        if(picker.ShowDialog(this)==true)EditorPath.Text=picker.FileName;
    }
    private async void Convert(object sender,RoutedEventArgs e)
    {
        if(modelPath==null||string.IsNullOrWhiteSpace(OutputPath.Text)){Append("Choose a source model and a new export folder first.");return;}
        var request=new ConversionRequest(modelPath,SkeletonPath.Text.Trim(),animationFiles.Select(f=>f.FullName).ToArray(),OutputPath.Text.Trim(),EditorPath.Text.Trim());
        cancellation=new CancellationTokenSource();Inputs.IsEnabled=false;ConvertButton.IsEnabled=false;CancelButton.IsEnabled=true;Progress.Value=0;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,JsonSerializer.Serialize(new Dictionary<string,string>{{"source",SourceFolder.Text},{"output",OutputPath.Text},{"editor",EditorPath.Text}}));
            var progress=new Progress<ConversionProgress>(p=>{Progress.Value=p.Percent;Status.Text=p.Message;Append(p.Message);});
            var result=await Task.Run(()=>new ConversionService().ConvertAsync(request,progress,cancellation.Token));
            Status.Text=$"Complete · {result.Vertices:N0} vertices · {result.Bones} bones · {result.Animations} animations";
            Append(result.BinaryValidated?"Acorn loaded and re-saved the binary resources successfully.":"Exported text resources; engine validation was not requested.");
        }
        catch(OperationCanceledException){Status.Text="Cancelled";Append("Conversion cancelled. Existing source and export files were preserved.");}
        catch(Exception ex){Status.Text="Conversion failed — see log";Append(ex.ToString());}
        finally{cancellation.Dispose();cancellation=null;Inputs.IsEnabled=true;ConvertButton.IsEnabled=true;CancelButton.IsEnabled=false;}
    }
    private void Cancel(object sender,RoutedEventArgs e){CancelButton.IsEnabled=false;cancellation?.Cancel();Status.Text="Cancelling…";}
    private void SaveLog(object sender,RoutedEventArgs e)
    {
        var picker=new SaveFileDialog{Filter="Text log|*.log",FileName="HE1ToSCU.log"};
        if(picker.ShowDialog(this)==true)TryUi(()=>File.WriteAllText(picker.FileName,Log.Text));
    }
    private void TryUi(Action action){try{action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Append(e.Message);}}
}
