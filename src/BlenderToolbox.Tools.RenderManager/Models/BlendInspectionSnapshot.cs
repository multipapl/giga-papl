namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class BlendInspectionSnapshot
{
    public List<string> AvailableCameras { get; set; } = [];

    public List<string> AvailableCollections { get; set; } = [];

    public List<string> AvailableScenes { get; set; } = [];

    public List<string> AvailableViewLayers { get; set; } = [];

    public string CameraName { get; set; } = string.Empty;

    public int FrameEnd { get; set; }

    public long BlendFileSizeBytes { get; set; }

    public DateTimeOffset? BlendFileLastWriteUtc { get; set; }

    public int FrameStart { get; set; }

    public int FrameStep { get; set; } = 1;

    public DateTimeOffset InspectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string OutputFormat { get; set; } = string.Empty;

    public string RawOutputPath { get; set; } = string.Empty;

    public string ResolvedOutputPath { get; set; } = string.Empty;

    public RendersetInspectionSnapshot Renderset { get; set; } = new();

    public Dictionary<string, List<string>> SceneCameras { get; set; } = [];

    public Dictionary<string, List<string>> SceneCollections { get; set; } = [];

    public string SceneName { get; set; } = string.Empty;

    public Dictionary<string, List<string>> SceneViewLayers { get; set; } = [];

    public string ViewLayerName { get; set; } = string.Empty;
}
