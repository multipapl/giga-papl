using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Services;
using BlenderToolbox.Tools.SplitByContext.Services;
using BlenderToolbox.Tools.SplitByContext.ViewModels;
using BlenderToolbox.Tools.SplitByContext.Views;

namespace BlenderToolbox.Tools.SplitByContext;

public sealed class SplitByContextTool : IToolDefinition, IStatefulTool
{
    private readonly SplitByContextViewModel _viewModel;

    public SplitByContextTool(
        IJsonSettingsStore settingsStore,
        GlobalSettingsService globalSettingsService,
        IFilePickerService filePickerService)
    {
        _viewModel = new SplitByContextViewModel(
            new SplitByContextService(),
            settingsStore,
            globalSettingsService,
            filePickerService);

        View = new SplitByContextView
        {
            DataContext = _viewModel,
        };
    }

    public string Description => "Split a scene file into one file per active context by running the executable headlessly.";

    public string DisplayName => "Split By Context";

    public string Id => "split-by-context";

    public object View { get; }

    public void SaveState()
    {
        _viewModel.SaveSettings();
    }
}
