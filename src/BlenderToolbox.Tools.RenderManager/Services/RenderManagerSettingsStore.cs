using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderManagerSettingsStore
{
    private readonly IJsonSettingsStore _settingsStore;

    public RenderManagerSettingsStore(IJsonSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public RenderManagerSettings Load()
    {
        return _settingsStore.Load<RenderManagerSettings>(RenderManagerPaths.SettingsFileName);
    }

    public void Save(RenderManagerSettings settings)
    {
        _settingsStore.Save(RenderManagerPaths.SettingsFileName, settings);
    }
}
