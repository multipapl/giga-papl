using System.Windows;
using BlenderToolbox.App.Services;
using BlenderToolbox.App.ViewModels;
using BlenderToolbox.Core.Services;
using BlenderToolbox.Tools.LazyFrameRename;
using BlenderToolbox.Tools.RenderManager;
using BlenderToolbox.Tools.SplitByContext;

namespace BlenderToolbox.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settingsStore = new JsonSettingsStore("BlenderToolbox");
        var folderPickerService = new FolderPickerService();
        var filePickerService = new FilePickerService();

        DataContext = new MainWindowViewModel(
        [
            new LazyFrameRenameTool(settingsStore, folderPickerService),
            new RenderManagerTool(settingsStore, filePickerService),
            new SplitByContextTool(settingsStore, filePickerService),
        ]);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveState();
        }

        base.OnClosing(e);
    }
}
