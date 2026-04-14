namespace BlenderToolbox.Tools.RenderManager.Models;

public enum RenderJobStatus
{
    Pending,
    Inspecting,
    Ready,
    Rendering,
    Stopping,
    Completed,
    Failed,
    Canceled,
    Skipped,
}
