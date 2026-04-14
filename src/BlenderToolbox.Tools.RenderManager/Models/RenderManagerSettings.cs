namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RenderManagerSettings
{
    public bool AutoInspectOnAdd { get; set; } = true;

    public string DefaultBlenderPath { get; set; } = string.Empty;

    public string DefaultOutputFileNameTemplate { get; set; } = "[BLEND_NAME]_[FRAME]";

    public string DefaultOutputPathTemplate { get; set; } = "[BLEND_PATH]\\renders";

    public string LastBlendDirectory { get; set; } = string.Empty;

    public string LastBlenderDirectory { get; set; } = string.Empty;
}
