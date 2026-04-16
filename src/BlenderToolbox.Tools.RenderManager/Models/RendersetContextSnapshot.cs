namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RendersetContextSnapshot
{
    public int Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IncludeInRenderAll { get; set; }

    public string RenderType { get; set; } = string.Empty;

    public string CameraName { get; set; } = string.Empty;

    public string OutputFolderHint { get; set; } = string.Empty;
}
