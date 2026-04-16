using System.Windows;
using BlenderToolbox.App.Services;
using BlenderToolbox.App.ViewModels;
using BlenderToolbox.App.Views;
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
        var globalSettingsService = ((App)Application.Current).GlobalSettingsService
            ?? new GlobalSettingsService(settingsStore);
        var folderPickerService = new FolderPickerService();
        var filePickerService = new FilePickerService();
        var settingsScreen = new SettingsScreenViewModel(globalSettingsService, filePickerService);
        settingsScreen.View = new SettingsScreenView
        {
            DataContext = settingsScreen,
        };

        DataContext = new MainWindowViewModel(
        [
            new RenderManagerTool(settingsStore, globalSettingsService, filePickerService),
            new LazyFrameRenameTool(settingsStore, folderPickerService),
            new SplitByContextTool(settingsStore, globalSettingsService, filePickerService),
        ],
        settingsScreen);
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
