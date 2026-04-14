namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RenderQueueItem
{
    public string BlendFilePath { get; set; } = string.Empty;

    public string BlenderExecutablePath { get; set; } = string.Empty;

    public string CameraName { get; set; } = string.Empty;

    public string CollectionOverrides { get; set; } = string.Empty;

    public string ElapsedText { get; set; } = string.Empty;

    public string EndFrame { get; set; } = string.Empty;

    public string EtaText { get; set; } = string.Empty;

    public string ExtraArgs { get; set; } = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastCompletedUtc { get; set; }

    public string LastErrorSummary { get; set; } = string.Empty;

    public string LastKnownOutputPath { get; set; } = string.Empty;

    public DateTimeOffset? LastStartedUtc { get; set; }

    public string LogOutput { get; set; } = string.Empty;

    public RenderMode Mode { get; set; } = RenderMode.Animation;

    public string Name { get; set; } = string.Empty;

    public string OutputFileNameTemplate { get; set; } = "[BLEND_NAME]_[FRAME]";

    public string OutputPathTemplate { get; set; } = "[BLEND_PATH]\\renders";

    public double ProgressValue { get; set; }

    public string ProgressText { get; set; } = "Waiting";

    public string SceneName { get; set; } = string.Empty;

    public string SingleFrame { get; set; } = string.Empty;

    public string StartFrame { get; set; } = string.Empty;

    public RenderJobStatus Status { get; set; } = RenderJobStatus.Pending;

    public string Step { get; set; } = "1";

    public string ViewLayerName { get; set; } = string.Empty;
}
