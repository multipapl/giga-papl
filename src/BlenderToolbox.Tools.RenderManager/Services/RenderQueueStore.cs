using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderQueueStore
{
    private readonly IJsonSettingsStore _settingsStore;

    public RenderQueueStore(IJsonSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public RenderQueueState Load()
    {
        return _settingsStore.Load<RenderQueueState>(RenderManagerPaths.QueueFileName);
    }

    public void Save(RenderQueueState queueState)
    {
        _settingsStore.Save(RenderManagerPaths.QueueFileName, queueState);
    }
}
