using System.Windows;
using System.Windows.Controls;
using HE1ToSCU.App;
using HE1ToSCU.Core;
using Xunit;

namespace HE1ToSCU.GuiTests;

public sealed class GameSelectionTests
{
    [Fact]
    public void ExistingWindowLoadsAndGameSelectionFeedsTheConversionProfile()
    {
        Exception? failure=null;
        var thread=new Thread(()=> {
            try {
                var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                var window=new MainWindow();
                var selector=Assert.IsType<ComboBox>(window.FindName("GameSelector"));
                Assert.Equal(2,selector.Items.Count);
                Assert.Equal("Sonic Generations",Assert.IsType<ComboBoxItem>(selector.Items[0]).Content);
                Assert.Equal("Sonic Unleashed",Assert.IsType<ComboBoxItem>(selector.Items[1]).Content);
                selector.SelectedIndex=0;Assert.Equal(SourceGame.SonicGenerations,window.SelectedGame);
                selector.SelectedIndex=1;Assert.Equal(SourceGame.SonicUnleashed,window.SelectedGame);
                Assert.NotNull(window.FindName("Models"));Assert.NotNull(window.FindName("Animations"));
                Assert.NotNull(window.FindName("SourceFolder"));Assert.NotNull(window.FindName("OutputPath"));
                var inputs=Assert.IsType<Grid>(window.FindName("Inputs"));
                var folderButtons=inputs.Children.OfType<Button>().Where(b=>Equals(b.Content,"Folder…")).ToArray();
                Assert.Contains(folderButtons,b=>Grid.GetRow(b)==Grid.GetRow((TextBox)window.FindName("SourceFolder")));
                Assert.DoesNotContain(folderButtons,b=>Grid.GetRow(b)==Grid.GetRow(selector));
                window.Close();application.Shutdown();
            } catch(Exception e) {failure=e;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
    }
}
