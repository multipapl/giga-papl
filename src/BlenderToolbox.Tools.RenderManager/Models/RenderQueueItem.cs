namespace BlenderToolbox.Tools.RenderManager.Models;

public sealed class RenderQueueItem
{
    public string BlendFilePath { get; set; } = string.Empty;

    public string BlenderExecutablePath { get; set; } = string.Empty;

    public string CameraName { get; set; } = string.Empty;

    public bool CameraOverrideEnabled { get; set; }

    public double CompletedFrameRenderSeconds { get; set; }

    public string ElapsedText { get; set; } = string.Empty;

    public string EndFrame { get; set; } = string.Empty;

    public string EtaText { get; set; } = string.Empty;

    public bool FrameOverrideEnabled { get; set; }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public BlendInspectionSnapshot? Inspection { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastCompletedUtc { get; set; }

    public int LastCompletedFrameNumber { get; set; }

    public string LastErrorSummary { get; set; } = string.Empty;

    public string LastKnownOutputPath { get; set; } = string.Empty;

    public string LastKnownOutputFolderPath { get; set; } = string.Empty;

    public string LastLogFilePath { get; set; } = string.Empty;

    public int LastReportedFrameNumber { get; set; }

    public DateTimeOffset? LastStartedUtc { get; set; }

    public string LogOutput { get; set; } = string.Empty;

    public RenderMode Mode { get; set; } = RenderMode.Animation;

    public string Name { get; set; } = string.Empty;

    public string OutputFileNameTemplate { get; set; } = "[BLEND_NAME]_[FRAME]";

    public bool OutputFileNameOverrideEnabled { get; set; }

    public string OutputPathTemplate { get; set; } = "[BLEND_PATH]\\renders";

    public bool OutputPathOverrideEnabled { get; set; }

    public double ProgressValue { get; set; }

    public string ProgressText { get; set; } = "Waiting";

    public int ResumeCompletedFrameCount { get; set; }

    public string SceneName { get; set; } = string.Empty;

    public bool SceneOverrideEnabled { get; set; }

    public string SingleFrame { get; set; } = string.Empty;

    public string StartFrame { get; set; } = string.Empty;

    public RenderJobStatus Status { get; set; } = RenderJobStatus.Pending;

    public string Step { get; set; } = "1";

    public List<string> SelectedRendersetContextNames { get; set; } = [];

    public bool UseRenderset { get; set; }

    public string ViewLayerName { get; set; } = string.Empty;

    public bool ViewLayerOverrideEnabled { get; set; }
}
