using System.IO;
using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public partial class RenderQueueItemViewModel : ObservableObject
{
    public RenderQueueItemViewModel()
    {
    }

    private RenderQueueItemViewModel(RenderQueueItem item)
    {
        Id = item.Id;
        IsEnabled = item.IsEnabled;
        Name = item.Name;
        BlendFilePath = item.BlendFilePath;
        BlenderExecutablePath = item.BlenderExecutablePath;
        Mode = item.Mode;
        StartFrame = item.StartFrame;
        EndFrame = item.EndFrame;
        Step = item.Step;
        SingleFrame = item.SingleFrame;
        SceneName = item.SceneName;
        CameraName = item.CameraName;
        ViewLayerName = item.ViewLayerName;
        CollectionOverrides = item.CollectionOverrides;
        OutputPathTemplate = item.OutputPathTemplate;
        OutputFileNameTemplate = item.OutputFileNameTemplate;
        ExtraArgs = item.ExtraArgs;
        Status = item.Status;
        ProgressValue = item.ProgressValue;
        ProgressText = item.ProgressText;
        ElapsedText = item.ElapsedText;
        EtaText = item.EtaText;
        LastKnownOutputPath = item.LastKnownOutputPath;
        LastErrorSummary = item.LastErrorSummary;
        LogOutput = item.LogOutput;
        LastStartedUtc = item.LastStartedUtc;
        LastCompletedUtc = item.LastCompletedUtc;
    }

    public string EffectiveName => string.IsNullOrWhiteSpace(Name)
        ? BlendFileName
        : Name.Trim();

    public string BlendDirectory => string.IsNullOrWhiteSpace(BlendFilePath)
        ? string.Empty
        : Path.GetDirectoryName(BlendFilePath.Trim()) ?? string.Empty;

    public string BlendFileName => string.IsNullOrWhiteSpace(BlendFilePath)
        ? "Untitled job"
        : Path.GetFileNameWithoutExtension(BlendFilePath.Trim());

    public string FrameSummary => Mode switch
    {
        RenderMode.Animation => "Animation",
        RenderMode.FrameRange => $"Frames {DisplayValue(StartFrame)} to {DisplayValue(EndFrame)} step {DisplayValue(Step, "1")}",
        RenderMode.SingleFrame => $"Frame {DisplayValue(SingleFrame)}",
        _ => "Mode pending",
    };

    public string ProgressPercentLabel => $"{ProgressValue:0}%";

    public string TargetSummary
    {
        get
        {
            var targets = new List<string>();

            if (!string.IsNullOrWhiteSpace(SceneName))
            {
                targets.Add($"Scene: {SceneName.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(CameraName))
            {
                targets.Add($"Camera: {CameraName.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(ViewLayerName))
            {
                targets.Add($"Layer: {ViewLayerName.Trim()}");
            }

            return targets.Count == 0
                ? "Targets inherit from the blend file."
                : string.Join(" | ", targets);
        }
    }

    public static RenderQueueItemViewModel CreateNew(
        string blendFilePath,
        string defaultBlenderPath,
        string defaultOutputPathTemplate,
        string defaultOutputFileNameTemplate)
    {
        var fileName = string.IsNullOrWhiteSpace(blendFilePath)
            ? "New render job"
            : Path.GetFileNameWithoutExtension(blendFilePath);

        return new RenderQueueItemViewModel
        {
            Name = fileName,
            BlendFilePath = blendFilePath,
            BlenderExecutablePath = defaultBlenderPath,
            OutputPathTemplate = defaultOutputPathTemplate,
            OutputFileNameTemplate = defaultOutputFileNameTemplate,
            ProgressValue = 0,
            ProgressText = "Waiting",
            LogOutput = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Queue item created.{Environment.NewLine}Milestone 1 shell is ready. Blend inspection, command execution, and live logs land in the next milestones.",
            Status = string.IsNullOrWhiteSpace(blendFilePath) ? RenderJobStatus.Pending : RenderJobStatus.Ready,
        };
    }

    public static RenderQueueItemViewModel FromModel(RenderQueueItem item)
    {
        return new RenderQueueItemViewModel(item);
    }

    public RenderQueueItemViewModel CreateDuplicate()
    {
        var duplicate = ToModel();
        duplicate.Id = Guid.NewGuid().ToString("N");
        duplicate.Name = $"{EffectiveName} Copy";
        duplicate.ProgressValue = 0;
        duplicate.ProgressText = "Waiting";
        duplicate.Status = RenderJobStatus.Pending;
        duplicate.LogOutput = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Queue item duplicated from {EffectiveName}.{Environment.NewLine}Milestone 1 shell only persists queue state; real render session logs will appear here later.";
        duplicate.LastStartedUtc = null;
        duplicate.LastCompletedUtc = null;
        duplicate.LastErrorSummary = string.Empty;

        return FromModel(duplicate);
    }

    public RenderQueueItem ToModel()
    {
        return new RenderQueueItem
        {
            Id = Id,
            IsEnabled = IsEnabled,
            Name = Name.Trim(),
            BlendFilePath = BlendFilePath.Trim(),
            BlenderExecutablePath = BlenderExecutablePath.Trim(),
            Mode = Mode,
            StartFrame = StartFrame.Trim(),
            EndFrame = EndFrame.Trim(),
            Step = Step.Trim(),
            SingleFrame = SingleFrame.Trim(),
            SceneName = SceneName.Trim(),
            CameraName = CameraName.Trim(),
            ViewLayerName = ViewLayerName.Trim(),
            CollectionOverrides = CollectionOverrides.Trim(),
            OutputPathTemplate = OutputPathTemplate.Trim(),
            OutputFileNameTemplate = OutputFileNameTemplate.Trim(),
            ExtraArgs = ExtraArgs.Trim(),
            Status = Status,
            ProgressValue = ProgressValue,
            ProgressText = ProgressText.Trim(),
            ElapsedText = ElapsedText.Trim(),
            EtaText = EtaText.Trim(),
            LastKnownOutputPath = LastKnownOutputPath.Trim(),
            LastErrorSummary = LastErrorSummary.Trim(),
            LogOutput = LogOutput ?? string.Empty,
            LastStartedUtc = LastStartedUtc,
            LastCompletedUtc = LastCompletedUtc,
        };
    }

    [ObservableProperty]
    private string blendFilePath = string.Empty;

    [ObservableProperty]
    private string blenderExecutablePath = string.Empty;

    [ObservableProperty]
    private string cameraName = string.Empty;

    [ObservableProperty]
    private string collectionOverrides = string.Empty;

    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string endFrame = string.Empty;

    [ObservableProperty]
    private string etaText = string.Empty;

    [ObservableProperty]
    private string extraArgs = string.Empty;

    [ObservableProperty]
    private string id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private DateTimeOffset? lastCompletedUtc;

    [ObservableProperty]
    private string lastErrorSummary = string.Empty;

    [ObservableProperty]
    private string lastKnownOutputPath = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? lastStartedUtc;

    [ObservableProperty]
    private string logOutput = string.Empty;

    [ObservableProperty]
    private RenderMode mode = RenderMode.Animation;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string outputFileNameTemplate = "[BLEND_NAME]_[FRAME]";

    [ObservableProperty]
    private string outputPathTemplate = "[BLEND_PATH]\\renders";

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string progressText = "Waiting";

    [ObservableProperty]
    private string sceneName = string.Empty;

    [ObservableProperty]
    private string singleFrame = string.Empty;

    [ObservableProperty]
    private string startFrame = string.Empty;

    [ObservableProperty]
    private RenderJobStatus status = RenderJobStatus.Pending;

    [ObservableProperty]
    private string step = "1";

    [ObservableProperty]
    private string viewLayerName = string.Empty;

    partial void OnBlendFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(BlendDirectory));
        OnPropertyChanged(nameof(BlendFileName));
        OnPropertyChanged(nameof(EffectiveName));
    }

    partial void OnCameraNameChanged(string value)
    {
        OnPropertyChanged(nameof(TargetSummary));
    }

    partial void OnEndFrameChanged(string value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnModeChanged(RenderMode value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(EffectiveName));
    }

    partial void OnProgressValueChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercentLabel));
    }

    partial void OnSceneNameChanged(string value)
    {
        OnPropertyChanged(nameof(TargetSummary));
    }

    partial void OnSingleFrameChanged(string value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnStartFrameChanged(string value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnStepChanged(string value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnViewLayerNameChanged(string value)
    {
        OnPropertyChanged(nameof(TargetSummary));
    }

    private static string DisplayValue(string? value, string fallback = "Auto")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
