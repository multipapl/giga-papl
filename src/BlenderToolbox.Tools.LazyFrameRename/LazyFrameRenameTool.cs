using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.LazyFrameRename.Services;
using BlenderToolbox.Tools.LazyFrameRename.ViewModels;
using BlenderToolbox.Tools.LazyFrameRename.Views;

namespace BlenderToolbox.Tools.LazyFrameRename;

public sealed class LazyFrameRenameTool : IToolDefinition, IStatefulTool
{
    private readonly LazyFrameRenameViewModel _viewModel;

    public LazyFrameRenameTool(IJsonSettingsStore settingsStore, IFolderPickerService folderPickerService)
    {
        _viewModel = new LazyFrameRenameViewModel(
            new FrameRenameService(),
            settingsStore,
            folderPickerService);

        View = new LazyFrameRenameView
        {
            DataContext = _viewModel,
        };
    }

    public string Description => "Batch rename image sequences while preserving the original start frame number.";

    public string DisplayName => "Lazy Frame Rename";

    public string Id => "lazy-frame-rename";

    public object View { get; }

    public void SaveState()
    {
        _viewModel.SaveSettings();
    }
}
