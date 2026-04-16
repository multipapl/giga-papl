namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RenderJobValidationResult
{
    public List<string> Errors { get; } = [];

    public bool IsValid => Errors.Count == 0;
}
