using System.ComponentModel;
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
            new JobRendersetViewModel(),
            new JobRuntimeViewModel())
    {
    }

    public RenderJobViewModel(
        JobHeaderViewModel header,
        JobBlenderViewModel blender,
        JobFramesViewModel frames,
        JobOutputViewModel output,
        JobTargetingViewModel targeting,
        JobRendersetViewModel renderset,
        JobRuntimeViewModel runtime)
    {
        Header = header;
        Blender = blender;
        Frames = frames;
        Output = output;
        Targeting = targeting;
        Renderset = renderset;
        Runtime = runtime;

        Header.PropertyChanged += OnChildPropertyChanged;
        Blender.PropertyChanged += OnChildPropertyChanged;
        Frames.PropertyChanged += OnChildPropertyChanged;
        Output.PropertyChanged += OnChildPropertyChanged;
        Targeting.PropertyChanged += OnChildPropertyChanged;
        Renderset.PropertyChanged += OnChildPropertyChanged;
        Runtime.PropertyChanged += OnChildPropertyChanged;
    }

    public JobHeaderViewModel Header { get; }

    public JobBlenderViewModel Blender { get; }

    public JobFramesViewModel Frames { get; }

    public JobOutputViewModel Output { get; }

    public JobTargetingViewModel Targeting { get; }

    public JobRendersetViewModel Renderset { get; }

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
        RenderMode.FrameRange => $"Frames {DisplayValue(ResolvedStartFrameText)} to {DisplayValue(ResolvedEndFrameText)} step {DisplayValue(ResolvedStepText, "1")}",
        RenderMode.SingleFrame => $"Frame {DisplayValue(ResolvedSingleFrameText)}",
        _ => "Mode pending",
    };

    public string FrameModeText => Frames.HasFrameOverride
        ? "Frame settings overridden"
        : "Using blend frame range";

    public bool UsesRenderset => Renderset.UseRenderset;

    public bool IsStandardRenderSettingsEnabled => !Renderset.UseRenderset;

    public string EffectiveOutputDirectory => Renderset.UseRenderset && !string.IsNullOrWhiteSpace(Runtime.LastKnownOutputFolderPath)
        ? Runtime.LastKnownOutputFolderPath.Trim()
        : ResolvedOutputDirectory;

    public string FrameStartInput
    {
        get => ResolvedStartFrameText;
        set
        {
            Frames.StartFrame = NormalizeFrameOverride(value, ResolveBlendStartFrameText());
            NotifyFrameInputChanged(nameof(FrameStartInput));
        }
    }

    public string FrameEndInput
    {
        get => ResolvedEndFrameText;
        set
        {
            Frames.EndFrame = NormalizeFrameOverride(value, ResolveBlendEndFrameText());
            NotifyFrameInputChanged(nameof(FrameEndInput));
        }
    }

    public string FrameStepInput
    {
        get => ResolvedStepText;
        set
        {
            Frames.Step = NormalizeFrameOverride(value, ResolveBlendStepText());
            NotifyFrameInputChanged(nameof(FrameStepInput));
        }
    }

    public string SingleFrameInput
    {
        get => ResolvedSingleFrameText;
        set
        {
            Frames.SingleFrame = NormalizeFrameOverride(value, ResolveBlendSingleFrameText());
            NotifyFrameInputChanged(nameof(SingleFrameInput));
        }
    }

    public string OutputDirectoryHint => OutputTemplateService.BuildOriginalOutputDirectoryHint(BuildTemplateContext());

    public string OutputNameHint => OutputTemplateService.BuildOriginalOutputNameHint(BuildTemplateContext());

    public string OutputPathModeText => Output.HasOutputPathOverride
        ? "Output path overridden"
        : JobOutputViewModel.BlenderDefaultLabel;

    public string OutputPathDisplayText
    {
        get
        {
            var value = Output.HasOutputPathOverride
                ? Output.OutputPathTemplate
                : ResolvedOutputDirectory;
            return string.IsNullOrWhiteSpace(value)
                ? JobOutputViewModel.BlenderDefaultLabel
                : value.Trim();
        }
    }

    public string OutputNameModeText => Output.HasOutputNameOverride
        ? "Render name overridden"
        : JobOutputViewModel.BlenderDefaultLabel;

    public string OutputNameInput
    {
        get => ResolvedOutputName;
        set
        {
            Output.OutputFileNameTemplate = NormalizeFrameOverride(value, OriginalOutputName);
            OnPropertyChanged(nameof(OutputNameInput));
            OnPropertyChanged(nameof(OutputNameModeText));
            OnPropertyChanged(nameof(ResolvedOutputName));
            OnPropertyChanged(nameof(ResolvedOutputPattern));
        }
    }

    public string OriginalOutputName => OutputTemplateService.ResolveOriginalOutputName(BuildTemplateContext());

    public string ResolvedOutputDirectory => OutputTemplateService.ResolveOutputDirectory(BuildTemplateContext());

    public string ResolvedOutputName => OutputTemplateService.ResolveOutputName(BuildTemplateContext());

    public string ResolvedOutputPattern => OutputTemplateService.BuildOutputPattern(BuildTemplateContext());

    public bool UsesOutputFallback => OutputTemplateService.UsesFallback(BuildTemplateContext());

    public string ResolvedStartFrameText => string.IsNullOrWhiteSpace(Frames.StartFrame)
        ? ResolveBlendStartFrameText()
        : Frames.StartFrame.Trim();

    public string ResolvedEndFrameText => string.IsNullOrWhiteSpace(Frames.EndFrame)
        ? ResolveBlendEndFrameText()
        : Frames.EndFrame.Trim();

    public string ResolvedSingleFrameText => string.IsNullOrWhiteSpace(Frames.SingleFrame)
        ? ResolveBlendSingleFrameText()
        : Frames.SingleFrame.Trim();

    public string ResolvedStepText => string.IsNullOrWhiteSpace(Frames.Step)
        ? ResolveBlendStepText()
        : Frames.Step.Trim();

    public static RenderJobViewModel CreateNew(string blendFilePath)
    {
        var job = new RenderJobViewModel();
        job.Header.BlendFilePath = blendFilePath;
        job.Blender.BlenderExecutablePath = string.Empty;
        job.Frames.Mode = RenderMode.FrameRange;
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
        job.Header.BlendFilePath = item.BlendFilePath;
        job.Blender.BlenderExecutablePath = item.BlenderExecutablePath;

        var hasFrameOverride = HasStoredFrameOverride(item);
        job.Frames.Mode = item.Mode == RenderMode.SingleFrame ? RenderMode.SingleFrame : RenderMode.FrameRange;
        job.Frames.StartFrame = hasFrameOverride ? item.StartFrame : string.Empty;
        job.Frames.EndFrame = hasFrameOverride ? item.EndFrame : string.Empty;
        job.Frames.Step = hasFrameOverride ? item.Step : string.Empty;
        job.Frames.SingleFrame = hasFrameOverride ? item.SingleFrame : string.Empty;
        job.Frames.FrameOverrideEnabled = hasFrameOverride;

        job.Targeting.SceneName = item.SceneName;
        job.Targeting.SceneOverrideEnabled = item.SceneOverrideEnabled || !string.IsNullOrWhiteSpace(item.SceneName);
        job.Targeting.CameraName = item.CameraName;
        job.Targeting.CameraOverrideEnabled = item.CameraOverrideEnabled || !string.IsNullOrWhiteSpace(item.CameraName);
        job.Targeting.ViewLayerName = item.ViewLayerName;
        job.Targeting.ViewLayerOverrideEnabled = item.ViewLayerOverrideEnabled || !string.IsNullOrWhiteSpace(item.ViewLayerName);
        job.Targeting.Inspection = item.Inspection;
        job.Targeting.InspectionState = item.Inspection is null ? InspectionState.NotInspected : InspectionState.Ready;
        job.Renderset.UseRenderset = item.UseRenderset;
        job.Renderset.InitializeSelection(item.SelectedRendersetContextNames, item.UseRenderset);
        job.Renderset.ApplyInspection(item.Inspection?.Renderset);

        job.Output.OutputPathTemplate = NormalizeLegacyOutputPathOverride(item.OutputPathTemplate);
        job.Output.OutputPathOverrideEnabled = job.Output.HasOutputPathOverride;
        job.Output.OutputFileNameTemplate = NormalizeLegacyOutputNameOverride(item.OutputFileNameTemplate);
        job.Output.OutputFileNameOverrideEnabled = job.Output.HasOutputNameOverride;

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
        job.Runtime.LastKnownOutputFolderPath = item.LastKnownOutputFolderPath;
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
            Name = string.Empty,
            BlendFilePath = Header.BlendFilePath.Trim(),
            BlenderExecutablePath = Blender.BlenderExecutablePath.Trim(),
            CameraOverrideEnabled = Targeting.CameraOverrideEnabled,
            Mode = Frames.Mode,
            FrameOverrideEnabled = Frames.HasFrameOverride,
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
            OutputPathOverrideEnabled = Output.HasOutputPathOverride,
            OutputFileNameTemplate = Output.OutputFileNameTemplate.Trim(),
            OutputFileNameOverrideEnabled = Output.HasOutputNameOverride,
            Status = Runtime.Status,
            ProgressValue = Runtime.ProgressValue,
            ProgressText = Runtime.ProgressText.Trim(),
            ElapsedText = Runtime.ElapsedText.Trim(),
            EtaText = Runtime.EtaText.Trim(),
            ResumeCompletedFrameCount = Runtime.ResumeCompletedFrameCount,
            LastReportedFrameNumber = Runtime.LastReportedFrameNumber,
            LastCompletedFrameNumber = Runtime.LastCompletedFrameNumber,
            LastKnownOutputPath = Runtime.LastKnownOutputPath.Trim(),
            LastKnownOutputFolderPath = Runtime.LastKnownOutputFolderPath.Trim(),
            LastLogFilePath = Runtime.LastLogFilePath.Trim(),
            LastErrorSummary = Runtime.LastErrorSummary.Trim(),
            LogOutput = Runtime.LogOutput ?? string.Empty,
            LastStartedUtc = Runtime.LastStartedUtc,
            LastCompletedUtc = Runtime.LastCompletedUtc,
            Inspection = Targeting.Inspection,
            UseRenderset = Renderset.UseRenderset,
            SelectedRendersetContextNames = Renderset.SelectedContextNames.ToList(),
        };
    }

    public RenderJobViewModel CreateDuplicate()
    {
        var duplicate = ToModel();
        duplicate.Id = Guid.NewGuid().ToString("N");
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
        Renderset.ApplyInspection(inspection.Renderset);
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
        OnPropertyChanged(nameof(FrameModeText));
        OnPropertyChanged(nameof(HasInspection));
        OnPropertyChanged(nameof(InspectionSummary));
        OnPropertyChanged(nameof(UsesRenderset));
        OnPropertyChanged(nameof(IsStandardRenderSettingsEnabled));
        OnPropertyChanged(nameof(OutputDirectoryHint));
        OnPropertyChanged(nameof(OutputNameHint));
        OnPropertyChanged(nameof(OutputPathModeText));
        OnPropertyChanged(nameof(OutputPathDisplayText));
        OnPropertyChanged(nameof(OutputNameModeText));
        OnPropertyChanged(nameof(OutputNameInput));
        OnPropertyChanged(nameof(OriginalOutputName));
        OnPropertyChanged(nameof(ResolvedEndFrameText));
        OnPropertyChanged(nameof(FrameEndInput));
        OnPropertyChanged(nameof(ResolvedOutputDirectory));
        OnPropertyChanged(nameof(EffectiveOutputDirectory));
        OnPropertyChanged(nameof(ResolvedOutputName));
        OnPropertyChanged(nameof(ResolvedOutputPattern));
        OnPropertyChanged(nameof(ResolvedSingleFrameText));
        OnPropertyChanged(nameof(SingleFrameInput));
        OnPropertyChanged(nameof(ResolvedStartFrameText));
        OnPropertyChanged(nameof(FrameStartInput));
        OnPropertyChanged(nameof(ResolvedStepText));
        OnPropertyChanged(nameof(FrameStepInput));
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

    private string ResolveBlendStartFrameText()
    {
        return Targeting.Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : string.Empty;
    }

    private string ResolveBlendEndFrameText()
    {
        return Targeting.Inspection is { FrameEnd: > 0 } inspection ? inspection.FrameEnd.ToString() : string.Empty;
    }

    private string ResolveBlendSingleFrameText()
    {
        return Targeting.Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : "1";
    }

    private string ResolveBlendStepText()
    {
        return Targeting.Inspection is { FrameStep: > 0 } inspection ? inspection.FrameStep.ToString() : "1";
    }

    private static string NormalizeFrameOverride(string value, string blendDefault)
    {
        var trimmedValue = value?.Trim() ?? string.Empty;
        var trimmedDefault = blendDefault?.Trim() ?? string.Empty;
        return string.Equals(trimmedValue, trimmedDefault, StringComparison.Ordinal)
            ? string.Empty
            : trimmedValue;
    }

    private void NotifyFrameInputChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(FrameModeText));
        OnPropertyChanged(nameof(FrameSummary));
    }

    private static string NormalizeStoredStep(string value)
    {
        return string.Equals(value?.Trim(), "1", StringComparison.Ordinal)
            ? string.Empty
            : value ?? string.Empty;
    }

    private static bool HasStoredFrameOverride(RenderQueueItem item)
    {
        return item.FrameOverrideEnabled
            || item.Mode == RenderMode.SingleFrame
            || !string.IsNullOrWhiteSpace(item.StartFrame)
            || !string.IsNullOrWhiteSpace(item.EndFrame)
            || !string.IsNullOrWhiteSpace(item.SingleFrame)
            || !string.IsNullOrWhiteSpace(NormalizeStoredStep(item.Step));
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
