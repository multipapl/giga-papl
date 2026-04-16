namespace BlenderToolbox.Tools.RenderManager.Models;

public enum RendersetRenderEventKind
{
    Job,
    Start,
    Frame,
    Done,
    Error,
    AllDone,
}

public sealed class RendersetRenderEvent
{
    public RendersetRenderEventKind Kind { get; set; }

    public int TotalContexts { get; set; }

    public int Index { get; set; } = -1;

    public string Name { get; set; } = string.Empty;

    public string RenderType { get; set; } = string.Empty;

    public int FrameStart { get; set; }

    public int FrameEnd { get; set; }

    public int FrameStep { get; set; } = 1;

    public int Frame { get; set; }

    public string Folder { get; set; } = string.Empty;

    public List<string> Folders { get; set; } = [];

    public string Error { get; set; } = string.Empty;
}
