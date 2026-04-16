namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RendersetInspectionSnapshot
{
    public bool HasRenderset { get; set; }

    public List<RendersetContextSnapshot> Contexts { get; set; } = [];

    public DateTimeOffset ProbedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
