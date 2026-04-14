namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RenderQueueState
{
    public List<RenderQueueItem> Items { get; set; } = [];

    public string SelectedJobId { get; set; } = string.Empty;
}
