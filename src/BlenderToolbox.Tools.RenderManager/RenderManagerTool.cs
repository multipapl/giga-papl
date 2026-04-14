using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels;
using BlenderToolbox.Tools.RenderManager.Views;

namespace BlenderToolbox.Tools.RenderManager;

public sealed class RenderManagerTool : IToolDefinition, IStatefulTool
{
    private readonly RenderManagerViewModel _viewModel;

    public RenderManagerTool(IJsonSettingsStore settingsStore, IFilePickerService filePickerService)
    {
        _viewModel = new RenderManagerViewModel(
            new RenderManagerSettingsStore(settingsStore),
            new RenderQueueStore(settingsStore),
            filePickerService);

        View = new RenderManagerView
        {
            DataContext = _viewModel,
        };
    }

    public string Description => "Build and persist a local Blender render queue before wiring real inspection and execution.";

    public string DisplayName => "Render Manager";

    public string Id => "render-manager";

    public object View { get; }

    public void SaveState()
    {
        _viewModel.SaveState();
    }
}
