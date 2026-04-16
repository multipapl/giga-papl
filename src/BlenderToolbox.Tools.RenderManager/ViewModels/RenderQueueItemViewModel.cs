using System.ComponentModel;
using System.Windows.Media.Imaging;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public sealed class RenderQueueItemViewModel : RenderJobViewModel
{
    public RenderQueueItemViewModel()
    {
        Header.PropertyChanged += OnFlatChildPropertyChanged;
        Blender.PropertyChanged += OnFlatChildPropertyChanged;
        Frames.PropertyChanged += OnFlatChildPropertyChanged;
        Output.PropertyChanged += OnFlatChildPropertyChanged;
        Targeting.PropertyChanged += OnFlatChildPropertyChanged;
        Runtime.PropertyChanged += OnFlatChildPropertyChanged;
    }

    private RenderQueueItemViewModel(RenderQueueItem item)
        : this()
    {
        Id = item.Id;
        IsEnabled = item.IsEnabled;
        BlendFilePath = item.BlendFilePath;
        BlenderExecutablePath = item.BlenderExecutablePath;
        var hasFrameOverride = HasStoredFrameOverride(item);
        Mode = item.Mode == RenderMode.SingleFrame ? RenderMode.SingleFrame : RenderMode.FrameRange;
        StartFrame = hasFrameOverride ? item.StartFrame : string.Empty;
        EndFrame = hasFrameOverride ? item.EndFrame : string.Empty;
        Step = hasFrameOverride ? item.Step : string.Empty;
        SingleFrame = hasFrameOverride ? item.SingleFrame : string.Empty;
        FrameOverrideEnabled = hasFrameOverride;
        SceneName = item.SceneName;
        SceneOverrideEnabled = item.SceneOverrideEnabled || !string.IsNullOrWhiteSpace(item.SceneName);
        CameraName = item.CameraName;
        CameraOverrideEnabled = item.CameraOverrideEnabled || !string.IsNullOrWhiteSpace(item.CameraName);
        ViewLayerName = item.ViewLayerName;
        ViewLayerOverrideEnabled = item.ViewLayerOverrideEnabled || !string.IsNullOrWhiteSpace(item.ViewLayerName);
        CompletedFrameRenderSeconds = item.CompletedFrameRenderSeconds;
        OutputPathTemplate = NormalizeLegacyOutputPathOverride(item.OutputPathTemplate);
        OutputPathOverrideEnabled = HasOutputPathOverride;
        OutputFileNameTemplate = NormalizeLegacyOutputNameOverride(item.OutputFileNameTemplate);
        OutputFileNameOverrideEnabled = HasOutputNameOverride;
        Status = item.Status;
        ProgressValue = item.ProgressValue;
        ProgressText = item.ProgressText;
        ElapsedText = item.ElapsedText;
        EtaText = item.EtaText;
        ResumeCompletedFrameCount = item.ResumeCompletedFrameCount;
        LastReportedFrameNumber = item.LastReportedFrameNumber;
        LastCompletedFrameNumber = item.LastCompletedFrameNumber;
        LastKnownOutputPath = item.LastKnownOutputPath;
        LastLogFilePath = item.LastLogFilePath;
        PreviewStatusText = string.IsNullOrWhiteSpace(item.LastKnownOutputPath)
            ? JobRuntimeViewModel.DefaultPreviewStatusText
            : JobRuntimeViewModel.StoredPreviewStatusText;
        LastErrorSummary = item.LastErrorSummary;
        LogOutput = item.LogOutput;
        LastStartedUtc = item.LastStartedUtc;
        LastCompletedUtc = item.LastCompletedUtc;
        Inspection = item.Inspection;
        InspectionState = Inspection is null ? InspectionState.NotInspected : InspectionState.Ready;
    }

    public string Name
    {
        get => Header.Name;
        set => Header.Name = value;
    }

    public bool IsEnabled
    {
        get => Header.IsEnabled;
        set => Header.IsEnabled = value;
    }

    public string BlendFilePath
    {
        get => Header.BlendFilePath;
        set => Header.BlendFilePath = value;
    }

    public string BlenderExecutablePath
    {
        get => Blender.BlenderExecutablePath;
        set => Blender.BlenderExecutablePath = value;
    }

    public RenderMode Mode
    {
        get => Frames.Mode;
        set => Frames.Mode = value;
    }

    public bool FrameOverrideEnabled
    {
        get => Frames.FrameOverrideEnabled;
        set => Frames.FrameOverrideEnabled = value;
    }

    public string StartFrame
    {
        get => Frames.StartFrame;
        set => Frames.StartFrame = value;
    }

    public string EndFrame
    {
        get => Frames.EndFrame;
        set => Frames.EndFrame = value;
    }

    public string Step
    {
        get => Frames.Step;
        set => Frames.Step = value;
    }

    public string SingleFrame
    {
        get => Frames.SingleFrame;
        set => Frames.SingleFrame = value;
    }

    public bool HasFrameOverride
    {
        get => Frames.HasFrameOverride;
        set
        {
            if (value == HasFrameOverride)
            {
                return;
            }

            Frames.FrameOverrideEnabled = value;
            if (!value)
            {
                Frames.Mode = RenderMode.FrameRange;
                Frames.StartFrame = string.Empty;
                Frames.EndFrame = string.Empty;
                Frames.SingleFrame = string.Empty;
                Frames.Step = string.Empty;
            }
            else
            {
                Frames.Mode = RenderMode.FrameRange;
                Frames.StartFrame = Inspection is { FrameStart: > 0 } inspectedStart ? inspectedStart.FrameStart.ToString() : Frames.StartFrame;
                Frames.EndFrame = Inspection is { FrameEnd: > 0 } inspectedEnd ? inspectedEnd.FrameEnd.ToString() : Frames.EndFrame;
                Frames.Step = Inspection is { FrameStep: > 0 } inspectedStep ? inspectedStep.FrameStep.ToString() : "1";
            }

            OnPropertyChanged(nameof(HasFrameOverride));
        }
    }

    public bool IsAnimationMode => Frames.IsAnimationMode;

    public bool IsFrameRangeMode => Frames.IsFrameRangeMode;

    public bool IsSingleFrameMode => Frames.IsSingleFrameMode;

    public string OutputPathTemplate
    {
        get => Output.OutputPathTemplate;
        set => Output.OutputPathTemplate = value;
    }

    public bool OutputPathOverrideEnabled
    {
        get => Output.OutputPathOverrideEnabled;
        set => Output.OutputPathOverrideEnabled = value;
    }

    public string OutputFileNameTemplate
    {
        get => Output.OutputFileNameTemplate;
        set => Output.OutputFileNameTemplate = value;
    }

    public bool OutputFileNameOverrideEnabled
    {
        get => Output.OutputFileNameOverrideEnabled;
        set => Output.OutputFileNameOverrideEnabled = value;
    }

    public bool HasOutputPathOverride
    {
        get => Output.HasOutputPathOverride;
        set => Output.SetOutputPathOverride(value, ResolvedOutputDirectory);
    }

    public bool HasOutputNameOverride
    {
        get => Output.HasOutputNameOverride;
        set => Output.SetOutputNameOverride(value, ResolvedOutputName);
    }

    public BlendInspectionSnapshot? Inspection
    {
        get => Targeting.Inspection;
        set => Targeting.Inspection = value;
    }

    public InspectionState InspectionState
    {
        get => Targeting.InspectionState;
        set => Targeting.InspectionState = value;
    }

    public string SceneName
    {
        get => Targeting.SceneName;
        set => Targeting.SceneName = value;
    }

    public bool SceneOverrideEnabled
    {
        get => Targeting.SceneOverrideEnabled;
        set => Targeting.SceneOverrideEnabled = value;
    }

    public bool HasSceneOverride
    {
        get => Targeting.HasSceneOverride;
        set => Targeting.HasSceneOverride = value;
    }

    public string CameraName
    {
        get => Targeting.CameraName;
        set => Targeting.CameraName = value;
    }

    public bool CameraOverrideEnabled
    {
        get => Targeting.CameraOverrideEnabled;
        set => Targeting.CameraOverrideEnabled = value;
    }

    public bool HasCameraOverride
    {
        get => Targeting.HasCameraOverride;
        set => Targeting.HasCameraOverride = value;
    }

    public string ViewLayerName
    {
        get => Targeting.ViewLayerName;
        set => Targeting.ViewLayerName = value;
    }

    public bool ViewLayerOverrideEnabled
    {
        get => Targeting.ViewLayerOverrideEnabled;
        set => Targeting.ViewLayerOverrideEnabled = value;
    }

    public bool HasViewLayerOverride
    {
        get => Targeting.HasViewLayerOverride;
        set => Targeting.HasViewLayerOverride = value;
    }

    public bool IsInspectionReady => Targeting.IsInspectionReady;

    public bool IsSceneSelectorEnabled => Targeting.IsSceneSelectorEnabled;

    public bool IsCameraSelectorEnabled => Targeting.IsCameraSelectorEnabled;

    public bool IsViewLayerSelectorEnabled => Targeting.IsViewLayerSelectorEnabled;

    public string ResolvedSceneName => Targeting.ResolvedSceneName;

    public string ResolvedCameraName => Targeting.ResolvedCameraName;

    public string ResolvedViewLayerName => Targeting.ResolvedViewLayerName;

    public IReadOnlyList<string> AvailableSceneNames => Targeting.AvailableSceneNames;

    public IReadOnlyList<string> AvailableCameraNames => Targeting.AvailableCameraNames;

    public IReadOnlyList<string> AvailableViewLayerNames => Targeting.AvailableViewLayerNames;

    public string SceneHint => Targeting.SceneHint;

    public string CameraHint => Targeting.CameraHint;

    public string ViewLayerHint => Targeting.ViewLayerHint;

    public RenderJobStatus Status
    {
        get => Runtime.Status;
        set => Runtime.Status = value;
    }

    public double ProgressValue
    {
        get => Runtime.ProgressValue;
        set => Runtime.ProgressValue = value;
    }

    public string ProgressText
    {
        get => Runtime.ProgressText;
        set => Runtime.ProgressText = value;
    }

    public string ProgressPercentLabel => Runtime.ProgressPercentLabel;

    public string ElapsedText
    {
        get => Runtime.ElapsedText;
        set => Runtime.ElapsedText = value;
    }

    public string EtaText
    {
        get => Runtime.EtaText;
        set => Runtime.EtaText = value;
    }

    public double CompletedFrameRenderSeconds
    {
        get => Runtime.CompletedFrameRenderSeconds;
        set => Runtime.CompletedFrameRenderSeconds = value;
    }

    public int ResumeCompletedFrameCount
    {
        get => Runtime.ResumeCompletedFrameCount;
        set => Runtime.ResumeCompletedFrameCount = value;
    }

    public int LastReportedFrameNumber
    {
        get => Runtime.LastReportedFrameNumber;
        set => Runtime.LastReportedFrameNumber = value;
    }

    public int LastCompletedFrameNumber
    {
        get => Runtime.LastCompletedFrameNumber;
        set => Runtime.LastCompletedFrameNumber = value;
    }

    public string LastKnownOutputPath
    {
        get => Runtime.LastKnownOutputPath;
        set => Runtime.LastKnownOutputPath = value;
    }

    public string LastLogFilePath
    {
        get => Runtime.LastLogFilePath;
        set => Runtime.LastLogFilePath = value;
    }

    public string LastErrorSummary
    {
        get => Runtime.LastErrorSummary;
        set => Runtime.LastErrorSummary = value;
    }

    public string LastErrorSummaryText => Runtime.LastErrorSummaryText;

    public string LogOutput
    {
        get => Runtime.LogOutput;
        set => Runtime.LogOutput = value;
    }

    public DateTimeOffset? LastStartedUtc
    {
        get => Runtime.LastStartedUtc;
        set => Runtime.LastStartedUtc = value;
    }

    public DateTimeOffset? LastCompletedUtc
    {
        get => Runtime.LastCompletedUtc;
        set => Runtime.LastCompletedUtc = value;
    }

    public BitmapSource? PreviewImageSource
    {
        get => Runtime.PreviewImageSource;
        set => Runtime.PreviewImageSource = value;
    }

    public string PreviewStatusText
    {
        get => Runtime.PreviewStatusText;
        set => Runtime.PreviewStatusText = value;
    }

    public bool HasPreviewImage => Runtime.HasPreviewImage;

    public string PreviewPathText => Runtime.PreviewPathText;

    public bool IsSelected
    {
        get => Runtime.IsSelected;
        set => Runtime.IsSelected = value;
    }

    public static new RenderQueueItemViewModel CreateNew(string blendFilePath)
    {
        var job = new RenderQueueItemViewModel
        {
            BlendFilePath = blendFilePath,
            BlenderExecutablePath = string.Empty,
            Mode = RenderMode.FrameRange,
            OutputPathTemplate = string.Empty,
            OutputFileNameTemplate = string.Empty,
        };

        job.ResetRuntimeState("Queue item created.");
        return job;
    }

    public static new RenderQueueItemViewModel FromModel(RenderQueueItem item)
    {
        return new RenderQueueItemViewModel(item);
    }

    public new RenderQueueItemViewModel CreateDuplicate()
    {
        var duplicate = ToModel();
        duplicate.Id = Guid.NewGuid().ToString("N");
        var duplicateViewModel = FromModel(duplicate);
        duplicateViewModel.ResetRuntimeState($"Queue item duplicated from {EffectiveName}.");
        duplicateViewModel.Status = RenderJobStatus.Pending;
        return duplicateViewModel;
    }

    public new void ApplyInspection(BlendInspectionSnapshot inspection)
    {
        Targeting.ApplyInspection(inspection, preserveSelections: false);
    }

    public void AppendBufferedLogLine(string line)
    {
        Runtime.AppendBufferedLogLine(line);
    }

    public bool FlushLogBuffer()
    {
        return Runtime.FlushLogBuffer();
    }

    public bool CanDecodePreviewNow(DateTimeOffset now, TimeSpan throttle)
    {
        return Runtime.CanDecodePreviewNow(now, throttle);
    }

    public void ResetRuntimeState(string? lifecycleMessage = null)
    {
        Runtime.ResetRuntimeState(BlendFilePath, lifecycleMessage);
    }

    private void OnFlatChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Header)
        {
            NotifyHeaderChanged(e.PropertyName);
        }
        else if (sender == Blender)
        {
            OnPropertyChanged(nameof(BlenderExecutablePath));
        }
        else if (sender == Frames)
        {
            NotifyFramesChanged(e.PropertyName);
        }
        else if (sender == Output)
        {
            NotifyOutputChanged(e.PropertyName);
        }
        else if (sender == Targeting)
        {
            NotifyTargetingChanged(e.PropertyName);
        }
        else if (sender == Runtime)
        {
            NotifyRuntimeChanged(e.PropertyName);
        }
    }

    private void NotifyHeaderChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(JobHeaderViewModel.Name):
            case nameof(JobHeaderViewModel.EffectiveName):
                OnPropertyChanged(nameof(Name));
                break;
            case nameof(JobHeaderViewModel.IsEnabled):
                OnPropertyChanged(nameof(IsEnabled));
                break;
            case nameof(JobHeaderViewModel.BlendFilePath):
            case nameof(JobHeaderViewModel.BlendDirectory):
            case nameof(JobHeaderViewModel.BlendFileName):
                OnPropertyChanged(nameof(BlendFilePath));
                break;
        }
    }

    private void NotifyFramesChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(JobFramesViewModel.Mode):
                OnPropertyChanged(nameof(Mode));
                OnPropertyChanged(nameof(IsAnimationMode));
                OnPropertyChanged(nameof(IsFrameRangeMode));
                OnPropertyChanged(nameof(IsSingleFrameMode));
                break;
            case nameof(JobFramesViewModel.FrameOverrideEnabled):
            case nameof(JobFramesViewModel.HasFrameOverride):
                OnPropertyChanged(nameof(FrameOverrideEnabled));
                OnPropertyChanged(nameof(HasFrameOverride));
                break;
            case nameof(JobFramesViewModel.StartFrame):
                OnPropertyChanged(nameof(StartFrame));
                break;
            case nameof(JobFramesViewModel.EndFrame):
                OnPropertyChanged(nameof(EndFrame));
                break;
            case nameof(JobFramesViewModel.Step):
                OnPropertyChanged(nameof(Step));
                break;
            case nameof(JobFramesViewModel.SingleFrame):
                OnPropertyChanged(nameof(SingleFrame));
                break;
        }
    }

    private void NotifyOutputChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(JobOutputViewModel.OutputPathTemplate):
                OnPropertyChanged(nameof(OutputPathTemplate));
                OnPropertyChanged(nameof(OutputPathModeText));
                OnPropertyChanged(nameof(OutputPathDisplayText));
                break;
            case nameof(JobOutputViewModel.OutputPathOverrideEnabled):
            case nameof(JobOutputViewModel.HasOutputPathOverride):
                OnPropertyChanged(nameof(OutputPathOverrideEnabled));
                OnPropertyChanged(nameof(HasOutputPathOverride));
                OnPropertyChanged(nameof(OutputPathModeText));
                OnPropertyChanged(nameof(OutputPathDisplayText));
                break;
            case nameof(JobOutputViewModel.OutputFileNameTemplate):
                OnPropertyChanged(nameof(OutputFileNameTemplate));
                OnPropertyChanged(nameof(OutputNameModeText));
                OnPropertyChanged(nameof(OutputNameInput));
                break;
            case nameof(JobOutputViewModel.OutputFileNameOverrideEnabled):
            case nameof(JobOutputViewModel.HasOutputNameOverride):
                OnPropertyChanged(nameof(OutputFileNameOverrideEnabled));
                OnPropertyChanged(nameof(HasOutputNameOverride));
                OnPropertyChanged(nameof(OutputNameModeText));
                OnPropertyChanged(nameof(OutputNameInput));
                break;
        }
    }

    private void NotifyTargetingChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(JobTargetingViewModel.Inspection):
                OnPropertyChanged(nameof(Inspection));
                break;
            case nameof(JobTargetingViewModel.InspectionState):
                OnPropertyChanged(nameof(InspectionState));
                OnPropertyChanged(nameof(IsInspectionReady));
                break;
            case nameof(JobTargetingViewModel.SceneName):
                OnPropertyChanged(nameof(SceneName));
                break;
            case nameof(JobTargetingViewModel.SceneOverrideEnabled):
            case nameof(JobTargetingViewModel.HasSceneOverride):
                OnPropertyChanged(nameof(SceneOverrideEnabled));
                OnPropertyChanged(nameof(HasSceneOverride));
                break;
            case nameof(JobTargetingViewModel.CameraName):
                OnPropertyChanged(nameof(CameraName));
                break;
            case nameof(JobTargetingViewModel.CameraOverrideEnabled):
            case nameof(JobTargetingViewModel.HasCameraOverride):
                OnPropertyChanged(nameof(CameraOverrideEnabled));
                OnPropertyChanged(nameof(HasCameraOverride));
                break;
            case nameof(JobTargetingViewModel.ViewLayerName):
                OnPropertyChanged(nameof(ViewLayerName));
                break;
            case nameof(JobTargetingViewModel.ViewLayerOverrideEnabled):
            case nameof(JobTargetingViewModel.HasViewLayerOverride):
                OnPropertyChanged(nameof(ViewLayerOverrideEnabled));
                OnPropertyChanged(nameof(HasViewLayerOverride));
                break;
            case nameof(JobTargetingViewModel.IsSceneSelectorEnabled):
                OnPropertyChanged(nameof(IsSceneSelectorEnabled));
                break;
            case nameof(JobTargetingViewModel.IsCameraSelectorEnabled):
                OnPropertyChanged(nameof(IsCameraSelectorEnabled));
                break;
            case nameof(JobTargetingViewModel.IsViewLayerSelectorEnabled):
                OnPropertyChanged(nameof(IsViewLayerSelectorEnabled));
                break;
        }
    }

    private void NotifyRuntimeChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(JobRuntimeViewModel.Status):
                OnPropertyChanged(nameof(Status));
                break;
            case nameof(JobRuntimeViewModel.ProgressValue):
            case nameof(JobRuntimeViewModel.ProgressPercentLabel):
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressPercentLabel));
                break;
            case nameof(JobRuntimeViewModel.ProgressText):
                OnPropertyChanged(nameof(ProgressText));
                break;
            case nameof(JobRuntimeViewModel.ElapsedText):
                OnPropertyChanged(nameof(ElapsedText));
                break;
            case nameof(JobRuntimeViewModel.EtaText):
                OnPropertyChanged(nameof(EtaText));
                break;
            case nameof(JobRuntimeViewModel.CompletedFrameRenderSeconds):
                OnPropertyChanged(nameof(CompletedFrameRenderSeconds));
                break;
            case nameof(JobRuntimeViewModel.ResumeCompletedFrameCount):
                OnPropertyChanged(nameof(ResumeCompletedFrameCount));
                break;
            case nameof(JobRuntimeViewModel.LastReportedFrameNumber):
                OnPropertyChanged(nameof(LastReportedFrameNumber));
                break;
            case nameof(JobRuntimeViewModel.LastCompletedFrameNumber):
                OnPropertyChanged(nameof(LastCompletedFrameNumber));
                break;
            case nameof(JobRuntimeViewModel.LastKnownOutputPath):
            case nameof(JobRuntimeViewModel.PreviewPathText):
                OnPropertyChanged(nameof(LastKnownOutputPath));
                OnPropertyChanged(nameof(PreviewPathText));
                break;
            case nameof(JobRuntimeViewModel.LastLogFilePath):
                OnPropertyChanged(nameof(LastLogFilePath));
                break;
            case nameof(JobRuntimeViewModel.LastErrorSummary):
            case nameof(JobRuntimeViewModel.LastErrorSummaryText):
                OnPropertyChanged(nameof(LastErrorSummary));
                OnPropertyChanged(nameof(LastErrorSummaryText));
                break;
            case nameof(JobRuntimeViewModel.LogOutput):
                OnPropertyChanged(nameof(LogOutput));
                break;
            case nameof(JobRuntimeViewModel.LastStartedUtc):
                OnPropertyChanged(nameof(LastStartedUtc));
                break;
            case nameof(JobRuntimeViewModel.LastCompletedUtc):
                OnPropertyChanged(nameof(LastCompletedUtc));
                break;
            case nameof(JobRuntimeViewModel.PreviewImageSource):
            case nameof(JobRuntimeViewModel.HasPreviewImage):
                OnPropertyChanged(nameof(PreviewImageSource));
                OnPropertyChanged(nameof(HasPreviewImage));
                break;
            case nameof(JobRuntimeViewModel.PreviewStatusText):
                OnPropertyChanged(nameof(PreviewStatusText));
                break;
            case nameof(JobRuntimeViewModel.IsSelected):
                OnPropertyChanged(nameof(IsSelected));
                break;
        }
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

    private static bool HasStoredFrameOverride(RenderQueueItem item)
    {
        return item.FrameOverrideEnabled
            || item.Mode == RenderMode.SingleFrame
            || !string.IsNullOrWhiteSpace(item.StartFrame)
            || !string.IsNullOrWhiteSpace(item.EndFrame)
            || !string.IsNullOrWhiteSpace(item.SingleFrame)
            || (!string.Equals(item.Step?.Trim(), "1", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(item.Step));
    }
}
