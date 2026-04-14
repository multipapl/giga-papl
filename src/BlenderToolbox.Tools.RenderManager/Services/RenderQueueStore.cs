using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderQueueStore
{
    private const string QueueFileName = "RenderManager\\queue.json";

    private readonly IJsonSettingsStore _settingsStore;

    public RenderQueueStore(IJsonSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public RenderQueueState Load()
    {
        return _settingsStore.Load<RenderQueueState>(QueueFileName);
    }

    public void Save(RenderQueueState queueState)
    {
        _settingsStore.Save(QueueFileName, queueState);
    }
}
