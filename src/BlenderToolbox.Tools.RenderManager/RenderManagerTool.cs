using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Services;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels;
using BlenderToolbox.Tools.RenderManager.Views;

namespace BlenderToolbox.Tools.RenderManager;

public sealed class RenderManagerTool : IToolDefinition, IStatefulTool
{
    private readonly RenderManagerViewModel _viewModel;

    public RenderManagerTool(
        IJsonSettingsStore settingsStore,
        GlobalSettingsService globalSettingsService,
        IFilePickerService filePickerService)
    {
        var paths = new RenderManagerPaths();
        var overrideScriptBuilder = new RenderOverrideScriptBuilder();

        _viewModel = new RenderManagerViewModel(
            new BlendInspectionService(paths),
            new RenderCommandBuilder(paths, overrideScriptBuilder),
            new RenderPreviewLoader(),
            new RenderManagerSettingsStore(settingsStore),
            new RenderQueueStore(settingsStore),
            new RenderJobValidationService(),
            new RenderJobLogWriter(),
            paths,
            globalSettingsService,
            filePickerService);

        View = new RenderManagerView
        {
            DataContext = _viewModel,
        };
    }

    public string Description => "Queue, inspect, and run local Blender renders with per-job overrides, logs, and resume support.";

    public string DisplayName => "Render Manager";

    public string Id => "render-manager";

    public object View { get; }

    public void SaveState()
    {
        _viewModel.SaveState();
    }
}
