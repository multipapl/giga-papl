using System.ComponentModel;
using System.IO;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class RenderJobViewModel : ObservableObject
{
    private static readonly RenderOutputTemplateService OutputTemplateService = new();

    public RenderJobViewModel()
        : this(
            new JobHeaderViewModel(),
            new JobBlenderViewModel(),
            new JobFramesViewModel(),
            new JobOutputViewModel(),
            new JobTargetingViewModel(),
            new JobRuntimeViewModel())
    {
    }

    public RenderJobViewModel(
        JobHeaderViewModel header,
        JobBlenderViewModel blender,
        JobFramesViewModel frames,
        JobOutputViewModel output,
        JobTargetingViewModel targeting,
        JobRuntimeViewModel runtime)
    {
        Header = header;
        Blender = blender;
        Frames = frames;
        Output = output;
        Targeting = targeting;
        Runtime = runtime;

        Header.PropertyChanged += OnChildPropertyChanged;
        Blender.PropertyChanged += OnChildPropertyChanged;
        Frames.PropertyChanged += OnChildPropertyChanged;
        Output.PropertyChanged += OnChildPropertyChanged;
        Targeting.PropertyChanged += OnChildPropertyChanged;
        Runtime.PropertyChanged += OnChildPropertyChanged;
    }

    public JobHeaderViewModel Header { get; }

    public JobBlenderViewModel Blender { get; }

    public JobFramesViewModel Frames { get; }

    public JobOutputViewModel Output { get; }

    public JobTargetingViewModel Targeting { get; }

    public JobRuntimeViewModel Runtime { get; }

    public string EffectiveName => Header.EffectiveName;

    public string BlendDirectory => Header.BlendDirectory;

    public string BlendFileName => Header.BlendFileName;

    public bool HasInspection => Targeting.HasInspection;

    public string InspectionSummary => Targeting.InspectionSummary;

    public string BlendFrameSummary
    {
        get
        {
            if (Targeting.Inspection is null || Targeting.Inspection.FrameStart <= 0 || Targeting.Inspection.FrameEnd <= 0)
            {
                return "Blend frame range will be used.";
            }

            var step = Math.Max(1, Targeting.Inspection.FrameStep);
            return $"Blend range {Targeting.Inspection.FrameStart} to {Targeting.Inspection.FrameEnd} step {step}";
        }
    }

    public string FrameSummary => Frames.Mode switch
    {
        RenderMode.Animation => $"Animation | {BlendFrameSummary}",
        RenderMode.FrameRange => $"Frames {DisplayValue(Frames.StartFrame)} to {DisplayValue(Frames.EndFrame)} step {DisplayValue(Frames.Step, "1")}",
        RenderMode.SingleFrame => $"Frame {DisplayValue(Frames.SingleFrame)}",
        _ => "Mode pending",
    };

    public string OutputDirectoryHint => OutputTemplateService.BuildOriginalOutputDirectoryHint(BuildTemplateContext());

    public string OutputNameHint => OutputTemplateService.BuildOriginalOutputNameHint(BuildTemplateContext());

    public string ResolvedOutputDirectory => OutputTemplateService.ResolveOutputDirectory(BuildTemplateContext());

    public string ResolvedOutputName => OutputTemplateService.ResolveOutputName(BuildTemplateContext());

    public string ResolvedOutputPattern => OutputTemplateService.BuildOutputPattern(BuildTemplateContext());

    public bool UsesOutputFallback => OutputTemplateService.UsesFallback(BuildTemplateContext());

    public string ResolvedStartFrameText => string.IsNullOrWhiteSpace(Frames.StartFrame)
        ? (Targeting.Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : string.Empty)
        : Frames.StartFrame.Trim();

    public string ResolvedEndFrameText => string.IsNullOrWhiteSpace(Frames.EndFrame)
        ? (Targeting.Inspection is { FrameEnd: > 0 } inspection ? inspection.FrameEnd.ToString() : string.Empty)
        : Frames.EndFrame.Trim();

    public string ResolvedSingleFrameText => string.IsNullOrWhiteSpace(Frames.SingleFrame)
        ? (Targeting.Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : "1")
        : Frames.SingleFrame.Trim();

    public string ResolvedStepText => string.IsNullOrWhiteSpace(Frames.Step)
        ? (Targeting.Inspection is { FrameStep: > 0 } inspection ? inspection.FrameStep.ToString() : "1")
        : Frames.Step.Trim();

    public static RenderJobViewModel CreateNew(string blendFilePath)
    {
        var fileName = string.IsNullOrWhiteSpace(blendFilePath)
            ? "New render job"
            : Path.GetFileNameWithoutExtension(blendFilePath);

        var job = new RenderJobViewModel();
        job.Header.Name = fileName;
        job.Header.BlendFilePath = blendFilePath;
        job.Blender.BlenderExecutablePath = string.Empty;
        job.Output.OutputPathTemplate = string.Empty;
        job.Output.OutputFileNameTemplate = string.Empty;
        job.Runtime.ResetRuntimeState(blendFilePath, "Queue item created.");
        return job;
    }

    public static RenderJobViewModel FromModel(RenderQueueItem item)
    {
        var job = new RenderJobViewModel
        {
            Id = item.Id,
        };

        job.Header.IsEnabled = item.IsEnabled;
        job.Header.Name = item.Name;
        job.Header.BlendFilePath = item.BlendFilePath;
        job.Blender.BlenderExecutablePath = item.BlenderExecutablePath;

        job.Frames.Mode = item.Mode;
        job.Frames.FrameOverrideEnabled = item.FrameOverrideEnabled || item.Mode != RenderMode.Animation;
        job.Frames.StartFrame = item.StartFrame;
        job.Frames.EndFrame = item.EndFrame;
        job.Frames.Step = item.Step;
        job.Frames.SingleFrame = item.SingleFrame;

        job.Targeting.SceneName = item.SceneName;
        job.Targeting.SceneOverrideEnabled = item.SceneOverrideEnabled || !string.IsNullOrWhiteSpace(item.SceneName);
        job.Targeting.CameraName = item.CameraName;
        job.Targeting.CameraOverrideEnabled = item.CameraOverrideEnabled || !string.IsNullOrWhiteSpace(item.CameraName);
        job.Targeting.ViewLayerName = item.ViewLayerName;
        job.Targeting.ViewLayerOverrideEnabled = item.ViewLayerOverrideEnabled || !string.IsNullOrWhiteSpace(item.ViewLayerName);
        job.Targeting.Inspection = item.Inspection;
        job.Targeting.InspectionState = item.Inspection is null ? InspectionState.NotInspected : InspectionState.Ready;

        job.Output.OutputPathTemplate = NormalizeLegacyOutputPathOverride(item.OutputPathTemplate);
        job.Output.OutputPathOverrideEnabled = item.OutputPathOverrideEnabled || !string.IsNullOrWhiteSpace(job.Output.OutputPathTemplate);
        job.Output.OutputFileNameTemplate = NormalizeLegacyOutputNameOverride(item.OutputFileNameTemplate);
        job.Output.OutputFileNameOverrideEnabled = item.OutputFileNameOverrideEnabled || !string.IsNullOrWhiteSpace(job.Output.OutputFileNameTemplate);

        job.Runtime.CompletedFrameRenderSeconds = item.CompletedFrameRenderSeconds;
        job.Runtime.Status = item.Status;
        job.Runtime.ProgressValue = item.ProgressValue;
        job.Runtime.ProgressText = item.ProgressText;
        job.Runtime.ElapsedText = item.ElapsedText;
        job.Runtime.EtaText = item.EtaText;
        job.Runtime.ResumeCompletedFrameCount = item.ResumeCompletedFrameCount;
        job.Runtime.LastReportedFrameNumber = item.LastReportedFrameNumber;
        job.Runtime.LastCompletedFrameNumber = item.LastCompletedFrameNumber;
        job.Runtime.LastKnownOutputPath = item.LastKnownOutputPath;
        job.Runtime.LastLogFilePath = item.LastLogFilePath;
        job.Runtime.PreviewStatusText = string.IsNullOrWhiteSpace(item.LastKnownOutputPath)
            ? JobRuntimeViewModel.DefaultPreviewStatusText
            : JobRuntimeViewModel.StoredPreviewStatusText;
        job.Runtime.LastErrorSummary = item.LastErrorSummary;
        job.Runtime.LogOutput = item.LogOutput;
        job.Runtime.LastStartedUtc = item.LastStartedUtc;
        job.Runtime.LastCompletedUtc = item.LastCompletedUtc;

        return job;
    }

    public RenderQueueItem ToModel()
    {
        return new RenderQueueItem
        {
            Id = Id,
            IsEnabled = Header.IsEnabled,
            Name = Header.Name.Trim(),
            BlendFilePath = Header.BlendFilePath.Trim(),
            BlenderExecutablePath = Blender.BlenderExecutablePath.Trim(),
            CameraOverrideEnabled = Targeting.CameraOverrideEnabled,
            Mode = Frames.Mode,
            FrameOverrideEnabled = Frames.FrameOverrideEnabled,
            StartFrame = Frames.StartFrame.Trim(),
            EndFrame = Frames.EndFrame.Trim(),
            Step = Frames.Step.Trim(),
            SingleFrame = Frames.SingleFrame.Trim(),
            SceneName = Targeting.SceneName.Trim(),
            SceneOverrideEnabled = Targeting.SceneOverrideEnabled,
            CameraName = Targeting.CameraName.Trim(),
            ViewLayerName = Targeting.ViewLayerName.Trim(),
            ViewLayerOverrideEnabled = Targeting.ViewLayerOverrideEnabled,
            CompletedFrameRenderSeconds = Runtime.CompletedFrameRenderSeconds,
            OutputPathTemplate = Output.OutputPathTemplate.Trim(),
            OutputPathOverrideEnabled = Output.OutputPathOverrideEnabled,
            OutputFileNameTemplate = Output.OutputFileNameTemplate.Trim(),
            OutputFileNameOverrideEnabled = Output.OutputFileNameOverrideEnabled,
            Status = Runtime.Status,
            ProgressValue = Runtime.ProgressValue,
            ProgressText = Runtime.ProgressText.Trim(),
            ElapsedText = Runtime.ElapsedText.Trim(),
            EtaText = Runtime.EtaText.Trim(),
            ResumeCompletedFrameCount = Runtime.ResumeCompletedFrameCount,
            LastReportedFrameNumber = Runtime.LastReportedFrameNumber,
            LastCompletedFrameNumber = Runtime.LastCompletedFrameNumber,
            LastKnownOutputPath = Runtime.LastKnownOutputPath.Trim(),
            LastLogFilePath = Runtime.LastLogFilePath.Trim(),
            LastErrorSummary = Runtime.LastErrorSummary.Trim(),
            LogOutput = Runtime.LogOutput ?? string.Empty,
            LastStartedUtc = Runtime.LastStartedUtc,
            LastCompletedUtc = Runtime.LastCompletedUtc,
            Inspection = Targeting.Inspection,
        };
    }

    public RenderJobViewModel CreateDuplicate()
    {
        var duplicate = ToModel();
        duplicate.Id = Guid.NewGuid().ToString("N");
        duplicate.Name = $"{EffectiveName} Copy";
        var duplicateViewModel = FromModel(duplicate);
        duplicateViewModel.Runtime.ResetRuntimeState(duplicateViewModel.Header.BlendFilePath, $"Queue item duplicated from {EffectiveName}.");
        duplicateViewModel.Runtime.Status = RenderJobStatus.Pending;
        return duplicateViewModel;
    }

    public CancellationToken BeginInspection()
    {
        return Targeting.BeginInspection();
    }

    public void ApplyInspection(BlendInspectionSnapshot inspection)
    {
        Targeting.ApplyInspection(inspection);
    }

    [ObservableProperty]
    private string id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private int queueIndex = 1;

    partial void OnQueueIndexChanged(int value)
    {
        NotifyResolvedSettingsChanged();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyResolvedSettingsChanged();
    }

    private void NotifyResolvedSettingsChanged()
    {
        OnPropertyChanged(nameof(EffectiveName));
        OnPropertyChanged(nameof(BlendDirectory));
        OnPropertyChanged(nameof(BlendFileName));
        OnPropertyChanged(nameof(BlendFrameSummary));
        OnPropertyChanged(nameof(FrameSummary));
        OnPropertyChanged(nameof(HasInspection));
        OnPropertyChanged(nameof(InspectionSummary));
        OnPropertyChanged(nameof(OutputDirectoryHint));
        OnPropertyChanged(nameof(OutputNameHint));
        OnPropertyChanged(nameof(ResolvedEndFrameText));
        OnPropertyChanged(nameof(ResolvedOutputDirectory));
        OnPropertyChanged(nameof(ResolvedOutputName));
        OnPropertyChanged(nameof(ResolvedOutputPattern));
        OnPropertyChanged(nameof(ResolvedSingleFrameText));
        OnPropertyChanged(nameof(ResolvedStartFrameText));
        OnPropertyChanged(nameof(ResolvedStepText));
        OnPropertyChanged(nameof(UsesOutputFallback));
    }

    private RenderQueueItemViewModelLike BuildTemplateContext()
    {
        return new RenderQueueItemViewModelLike(
            BlendDirectory,
            BlendFileName,
            Output.OutputPathTemplate,
            Output.OutputFileNameTemplate,
            Targeting.ResolvedSceneName,
            Targeting.ResolvedCameraName,
            Targeting.ResolvedViewLayerName,
            QueueIndex,
            Targeting.Inspection);
    }

    private static string DisplayValue(string? value, string fallback = "Auto")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeLegacyOutputNameOverride(string value)
    {
        return string.Equals(value?.Trim(), "[BLEND_NAME]_[FRAME]", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value ?? string.Empty;
    }

    private static string NormalizeLegacyOutputPathOverride(string value)
    {
        return string.Equals(value?.Trim(), "[BLEND_PATH]\\renders", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value ?? string.Empty;
    }
}
