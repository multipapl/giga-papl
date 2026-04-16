using System.IO;
using System.Windows.Media.Imaging;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public partial class RenderQueueItemViewModel : ObservableObject
{
    private const string DefaultPreviewStatusText = "Preview will appear after the first saved frame.";
    private const string StoredPreviewStatusText = "Preview can be reloaded from the last saved frame.";
    private static readonly RenderOutputTemplateService OutputTemplateService = new();

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
        FrameOverrideEnabled = item.FrameOverrideEnabled || item.Mode != RenderMode.Animation;
        StartFrame = item.StartFrame;
        EndFrame = item.EndFrame;
        Step = item.Step;
        SingleFrame = item.SingleFrame;
        SceneName = item.SceneName;
        SceneOverrideEnabled = item.SceneOverrideEnabled || !string.IsNullOrWhiteSpace(item.SceneName);
        CameraName = item.CameraName;
        CameraOverrideEnabled = item.CameraOverrideEnabled || !string.IsNullOrWhiteSpace(item.CameraName);
        ViewLayerName = item.ViewLayerName;
        ViewLayerOverrideEnabled = item.ViewLayerOverrideEnabled || !string.IsNullOrWhiteSpace(item.ViewLayerName);
        CollectionOverrides = item.CollectionOverrides;
        CollectionOverrideEnabled = item.CollectionOverrideEnabled || !string.IsNullOrWhiteSpace(item.CollectionOverrides);
        CompletedFrameRenderSeconds = item.CompletedFrameRenderSeconds;
        DeviceMode = item.DeviceMode;
        OutputPathTemplate = NormalizeLegacyOutputPathOverride(item.OutputPathTemplate);
        OutputPathOverrideEnabled = item.OutputPathOverrideEnabled || !string.IsNullOrWhiteSpace(OutputPathTemplate);
        OutputFileNameTemplate = NormalizeLegacyOutputNameOverride(item.OutputFileNameTemplate);
        OutputFileNameOverrideEnabled = item.OutputFileNameOverrideEnabled || !string.IsNullOrWhiteSpace(OutputFileNameTemplate);
        OutputFormat = item.OutputFormat;
        OutputFormatOverrideEnabled = item.OutputFormatOverrideEnabled || !string.IsNullOrWhiteSpace(item.OutputFormat);
        ExtraArgs = item.ExtraArgs;
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
            ? DefaultPreviewStatusText
            : StoredPreviewStatusText;
        LastErrorSummary = item.LastErrorSummary;
        LogOutput = item.LogOutput;
        LastStartedUtc = item.LastStartedUtc;
        LastCompletedUtc = item.LastCompletedUtc;
        Inspection = item.Inspection;
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

    public string BlendFrameSummary
    {
        get
        {
            if (Inspection is null || Inspection.FrameStart <= 0 || Inspection.FrameEnd <= 0)
            {
                return "Blend frame range will be used.";
            }

            var step = Math.Max(1, Inspection.FrameStep);
            return $"Blend range {Inspection.FrameStart} to {Inspection.FrameEnd} step {step}";
        }
    }

    public string FrameSummary => Mode switch
    {
        RenderMode.Animation => $"Animation | {BlendFrameSummary}",
        RenderMode.FrameRange => $"Frames {DisplayValue(StartFrame)} to {DisplayValue(EndFrame)} step {DisplayValue(Step, "1")}",
        RenderMode.SingleFrame => $"Frame {DisplayValue(SingleFrame)}",
        _ => "Mode pending",
    };

    public bool HasFrameOverride
    {
        get => FrameOverrideEnabled;
        set
        {
            if (value == HasFrameOverride)
            {
                return;
            }

            FrameOverrideEnabled = value;
            if (!value)
            {
                Mode = RenderMode.Animation;
                StartFrame = string.Empty;
                EndFrame = string.Empty;
                SingleFrame = string.Empty;
                Step = "1";
            }
            else
            {
                Mode = RenderMode.FrameRange;
                StartFrame = Inspection is { FrameStart: > 0 } inspectedStart ? inspectedStart.FrameStart.ToString() : StartFrame;
                EndFrame = Inspection is { FrameEnd: > 0 } inspectedEnd ? inspectedEnd.FrameEnd.ToString() : EndFrame;
                Step = Inspection is { FrameStep: > 0 } inspectedStep ? inspectedStep.FrameStep.ToString() : "1";
            }

            OnPropertyChanged(nameof(HasFrameOverride));
        }
    }

    public bool HasInspection => Inspection is not null;

    public bool HasCollectionOverride
    {
        get => CollectionOverrideEnabled;
        set
        {
            if (value == HasCollectionOverride)
            {
                return;
            }

            CollectionOverrideEnabled = value;
            if (!value)
            {
                CollectionOverrides = string.Empty;
            }

            OnPropertyChanged(nameof(HasCollectionOverride));
        }
    }

    public string InspectionSummary
    {
        get
        {
            if (Inspection is null)
            {
                return "Blend defaults are not inspected yet.";
            }

            return $"Inspected {Inspection.InspectedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        }
    }

    public bool IsAnimationMode => Mode == RenderMode.Animation;

    public bool IsFrameRangeMode => Mode == RenderMode.FrameRange;

    public bool IsSingleFrameMode => Mode == RenderMode.SingleFrame;

    public bool HasOutputFormatOverride
    {
        get => OutputFormatOverrideEnabled;
        set
        {
            if (value == HasOutputFormatOverride)
            {
                return;
            }

            OutputFormatOverrideEnabled = value;
            OutputFormat = value ? ResolvedOutputFormat : string.Empty;
            OnPropertyChanged(nameof(HasOutputFormatOverride));
        }
    }

    public bool HasOutputNameOverride
    {
        get => OutputFileNameOverrideEnabled;
        set
        {
            if (value == HasOutputNameOverride)
            {
                return;
            }

            OutputFileNameOverrideEnabled = value;
            OutputFileNameTemplate = value ? ResolvedOutputName : string.Empty;
            OnPropertyChanged(nameof(HasOutputNameOverride));
        }
    }

    public bool HasOutputPathOverride
    {
        get => OutputPathOverrideEnabled;
        set
        {
            if (value == HasOutputPathOverride)
            {
                return;
            }

            OutputPathOverrideEnabled = value;
            OutputPathTemplate = value ? ResolvedOutputDirectory : string.Empty;
            OnPropertyChanged(nameof(HasOutputPathOverride));
        }
    }

    public string OutputDirectoryHint
    {
        get => OutputTemplateService.BuildOriginalOutputDirectoryHint(BuildTemplateContext());
    }

    public string OutputNameHint
    {
        get => OutputTemplateService.BuildOriginalOutputNameHint(BuildTemplateContext());
    }

    public string OutputFormatHint => string.IsNullOrWhiteSpace(Inspection?.OutputFormat)
        ? "Empty = use format from blend."
        : $"Empty = from blend: {Inspection.OutputFormat}";

    public string ProgressPercentLabel => $"{ProgressValue:0}%";

    public bool HasPreviewImage => PreviewImageSource is not null;

    public string PreviewPathText => string.IsNullOrWhiteSpace(LastKnownOutputPath)
        ? "Waiting for the first saved frame."
        : LastKnownOutputPath.Trim();

    public string ResolvedCameraName => ResolveOverride(CameraName, Inspection?.CameraName);

    public string ResolvedOutputDirectory => OutputTemplateService.ResolveOutputDirectory(BuildTemplateContext());

    public string ResolvedOutputFormat => ResolveOverride(OutputFormat, Inspection?.OutputFormat);

    public string ResolvedOutputName => OutputTemplateService.ResolveOutputName(BuildTemplateContext());

    public string ResolvedOutputPattern => OutputTemplateService.BuildOutputPattern(BuildTemplateContext());

    public string CollectionHint => string.IsNullOrWhiteSpace(Inspection?.SceneName)
        ? "Exclude collection names separated by commas or new lines."
        : $"Exclude collections for {ResolvedSceneName}. Separate names with commas or new lines.";

    public string LastErrorSummaryText => string.IsNullOrWhiteSpace(LastErrorSummary)
        ? "No validation or runtime errors recorded."
        : LastErrorSummary.Trim();

    public string ResolvedSceneName => ResolveOverride(SceneName, Inspection?.SceneName);

    public string ResolvedViewLayerName => ResolveOverride(ViewLayerName, Inspection?.ViewLayerName);

    public bool HasSceneOverride
    {
        get => SceneOverrideEnabled;
        set
        {
            if (value == HasSceneOverride)
            {
                return;
            }

            SceneOverrideEnabled = value;
            SceneName = value ? ResolvedSceneName : string.Empty;
            OnPropertyChanged(nameof(HasSceneOverride));
        }
    }

    public bool HasCameraOverride
    {
        get => CameraOverrideEnabled;
        set
        {
            if (value == HasCameraOverride)
            {
                return;
            }

            CameraOverrideEnabled = value;
            CameraName = value ? ResolvedCameraName : string.Empty;
            OnPropertyChanged(nameof(HasCameraOverride));
        }
    }

    public bool HasViewLayerOverride
    {
        get => ViewLayerOverrideEnabled;
        set
        {
            if (value == HasViewLayerOverride)
            {
                return;
            }

            ViewLayerOverrideEnabled = value;
            ViewLayerName = value ? ResolvedViewLayerName : string.Empty;
            OnPropertyChanged(nameof(HasViewLayerOverride));
        }
    }

    public string SceneHint => BuildInheritedHint(Inspection?.SceneName, "scene");

    public string CameraHint => BuildInheritedHint(Inspection?.CameraName, "camera");

    public string ViewLayerHint => BuildInheritedHint(Inspection?.ViewLayerName, "view layer");

    public bool UsesOutputFallback => OutputTemplateService.UsesFallback(BuildTemplateContext());

    public string ResolvedEndFrameText => string.IsNullOrWhiteSpace(EndFrame)
        ? (Inspection is { FrameEnd: > 0 } inspection ? inspection.FrameEnd.ToString() : string.Empty)
        : EndFrame.Trim();

    public string ResolvedSingleFrameText => string.IsNullOrWhiteSpace(SingleFrame)
        ? (Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : "1")
        : SingleFrame.Trim();

    public string ResolvedStartFrameText => string.IsNullOrWhiteSpace(StartFrame)
        ? (Inspection is { FrameStart: > 0 } inspection ? inspection.FrameStart.ToString() : string.Empty)
        : StartFrame.Trim();

    public string ResolvedStepText => string.IsNullOrWhiteSpace(Step)
        ? (Inspection is { FrameStep: > 0 } inspection ? inspection.FrameStep.ToString() : "1")
        : Step.Trim();

    public static RenderQueueItemViewModel CreateNew(string blendFilePath)
    {
        var fileName = string.IsNullOrWhiteSpace(blendFilePath)
            ? "New render job"
            : Path.GetFileNameWithoutExtension(blendFilePath);

        var job = new RenderQueueItemViewModel
        {
            Name = fileName,
            BlendFilePath = blendFilePath,
            BlenderExecutablePath = string.Empty,
            OutputPathTemplate = string.Empty,
            OutputFileNameTemplate = string.Empty,
            OutputFormat = string.Empty,
        };

        job.ResetRuntimeState("Queue item created.");
        return job;
    }

    public static RenderQueueItemViewModel FromModel(RenderQueueItem item)
    {
        return new RenderQueueItemViewModel(item);
    }

    public void ApplyInspection(BlendInspectionSnapshot inspection)
    {
        Inspection = inspection;
    }

    public RenderQueueItemViewModel CreateDuplicate()
    {
        var duplicate = ToModel();
        duplicate.Id = Guid.NewGuid().ToString("N");
        duplicate.Name = $"{EffectiveName} Copy";
        var duplicateViewModel = FromModel(duplicate);
        duplicateViewModel.ResetRuntimeState($"Queue item duplicated from {EffectiveName}.");
        duplicateViewModel.Status = RenderJobStatus.Pending;
        return duplicateViewModel;
    }

    public void ResetRuntimeState(string? lifecycleMessage = null)
    {
        ProgressValue = 0;
        ProgressText = "Waiting";
        ElapsedText = string.Empty;
        EtaText = string.Empty;
        PreviewImageSource = null;
        PreviewStatusText = DefaultPreviewStatusText;
        LastKnownOutputPath = string.Empty;
        LastErrorSummary = string.Empty;
        CompletedFrameRenderSeconds = 0;
        ResumeCompletedFrameCount = 0;
        LastReportedFrameNumber = 0;
        LastCompletedFrameNumber = 0;
        LastStartedUtc = null;
        LastCompletedUtc = null;
        LogOutput = BuildLifecycleLog(lifecycleMessage ?? "Queue item reset.");
        Status = GetInitialStatus(BlendFilePath);
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
            CameraOverrideEnabled = CameraOverrideEnabled,
            Mode = Mode,
            FrameOverrideEnabled = FrameOverrideEnabled,
            StartFrame = StartFrame.Trim(),
            EndFrame = EndFrame.Trim(),
            Step = Step.Trim(),
            SingleFrame = SingleFrame.Trim(),
            SceneName = SceneName.Trim(),
            SceneOverrideEnabled = SceneOverrideEnabled,
            CameraName = CameraName.Trim(),
            ViewLayerName = ViewLayerName.Trim(),
            ViewLayerOverrideEnabled = ViewLayerOverrideEnabled,
            CollectionOverrides = CollectionOverrides.Trim(),
            CollectionOverrideEnabled = CollectionOverrideEnabled,
            CompletedFrameRenderSeconds = CompletedFrameRenderSeconds,
            DeviceMode = DeviceMode,
            OutputPathTemplate = OutputPathTemplate.Trim(),
            OutputPathOverrideEnabled = OutputPathOverrideEnabled,
            OutputFileNameTemplate = OutputFileNameTemplate.Trim(),
            OutputFileNameOverrideEnabled = OutputFileNameOverrideEnabled,
            OutputFormat = OutputFormat.Trim(),
            OutputFormatOverrideEnabled = OutputFormatOverrideEnabled,
            ExtraArgs = ExtraArgs.Trim(),
            Status = Status,
            ProgressValue = ProgressValue,
            ProgressText = ProgressText.Trim(),
            ElapsedText = ElapsedText.Trim(),
            EtaText = EtaText.Trim(),
            ResumeCompletedFrameCount = ResumeCompletedFrameCount,
            LastReportedFrameNumber = LastReportedFrameNumber,
            LastCompletedFrameNumber = LastCompletedFrameNumber,
            LastKnownOutputPath = LastKnownOutputPath.Trim(),
            LastLogFilePath = LastLogFilePath.Trim(),
            LastErrorSummary = LastErrorSummary.Trim(),
            LogOutput = LogOutput ?? string.Empty,
            LastStartedUtc = LastStartedUtc,
            LastCompletedUtc = LastCompletedUtc,
            Inspection = Inspection,
        };
    }

    [ObservableProperty]
    private string blendFilePath = string.Empty;

    [ObservableProperty]
    private string blenderExecutablePath = string.Empty;

    [ObservableProperty]
    private bool cameraOverrideEnabled;

    [ObservableProperty]
    private string cameraName = string.Empty;

    [ObservableProperty]
    private string collectionOverrides = string.Empty;

    [ObservableProperty]
    private bool collectionOverrideEnabled;

    [ObservableProperty]
    private double completedFrameRenderSeconds;

    [ObservableProperty]
    private RenderDeviceMode deviceMode = RenderDeviceMode.Default;

    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string endFrame = string.Empty;

    [ObservableProperty]
    private string etaText = string.Empty;

    [ObservableProperty]
    private string extraArgs = string.Empty;

    [ObservableProperty]
    private bool frameOverrideEnabled;

    [ObservableProperty]
    private string id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private BlendInspectionSnapshot? inspection;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private DateTimeOffset? lastCompletedUtc;

    [ObservableProperty]
    private int lastCompletedFrameNumber;

    [ObservableProperty]
    private string lastErrorSummary = string.Empty;

    [ObservableProperty]
    private string lastKnownOutputPath = string.Empty;

    [ObservableProperty]
    private string lastLogFilePath = string.Empty;

    [ObservableProperty]
    private int lastReportedFrameNumber;

    [ObservableProperty]
    private DateTimeOffset? lastStartedUtc;

    [ObservableProperty]
    private string logOutput = string.Empty;

    [ObservableProperty]
    private RenderMode mode = RenderMode.Animation;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int queueIndex = 1;

    [ObservableProperty]
    private BitmapSource? previewImageSource;

    [ObservableProperty]
    private string previewStatusText = DefaultPreviewStatusText;

    [ObservableProperty]
    private bool outputFileNameOverrideEnabled;

    [ObservableProperty]
    private string outputFileNameTemplate = string.Empty;

    [ObservableProperty]
    private bool outputFormatOverrideEnabled;

    [ObservableProperty]
    private string outputFormat = string.Empty;

    [ObservableProperty]
    private bool outputPathOverrideEnabled;

    [ObservableProperty]
    private string outputPathTemplate = string.Empty;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string progressText = "Waiting";

    [ObservableProperty]
    private int resumeCompletedFrameCount;

    [ObservableProperty]
    private string sceneName = string.Empty;

    [ObservableProperty]
    private bool sceneOverrideEnabled;

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

    [ObservableProperty]
    private bool viewLayerOverrideEnabled;

    public IReadOnlyList<string> AvailableCameraNames
    {
        get
        {
            if (Inspection?.SceneCameras is not { Count: > 0 } sceneCameras)
            {
                return Inspection?.AvailableCameras ?? [];
            }

            var sceneKey = ResolvedSceneName;
            if (!string.IsNullOrWhiteSpace(sceneKey) && sceneCameras.TryGetValue(sceneKey, out var cameras) && cameras.Count > 0)
            {
                return cameras;
            }

            return Inspection?.AvailableCameras ?? [];
        }
    }

    public IReadOnlyList<string> AvailableSceneNames => Inspection?.AvailableScenes ?? [];

    public IReadOnlyList<string> AvailableCollectionNames
    {
        get
        {
            if (Inspection?.SceneCollections is not { Count: > 0 } sceneCollections)
            {
                return Inspection?.AvailableCollections ?? [];
            }

            var sceneKey = ResolvedSceneName;
            if (!string.IsNullOrWhiteSpace(sceneKey) &&
                sceneCollections.TryGetValue(sceneKey, out var collections) &&
                collections.Count > 0)
            {
                return collections;
            }

            return Inspection?.AvailableCollections ?? [];
        }
    }

    public IReadOnlyList<string> AvailableViewLayerNames
    {
        get
        {
            if (Inspection?.SceneViewLayers is not { Count: > 0 } sceneViewLayers)
            {
                return Inspection?.AvailableViewLayers ?? [];
            }

            var sceneKey = ResolvedSceneName;
            if (!string.IsNullOrWhiteSpace(sceneKey) && sceneViewLayers.TryGetValue(sceneKey, out var viewLayers) && viewLayers.Count > 0)
            {
                return viewLayers;
            }

            return Inspection?.AvailableViewLayers ?? [];
        }
    }

    partial void OnBlendFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(BlendDirectory));
        OnPropertyChanged(nameof(BlendFileName));
        OnPropertyChanged(nameof(EffectiveName));
        NotifyResolvedSettingsChanged();
    }

    partial void OnCameraNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasCameraOverride));
        NotifyTargetingChanged();
    }

    partial void OnCollectionOverridesChanged(string value)
    {
        OnPropertyChanged(nameof(HasCollectionOverride));
    }

    partial void OnCollectionOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasCollectionOverride));
    }

    partial void OnDeviceModeChanged(RenderDeviceMode value)
    {
        NotifyResolvedSettingsChanged();
    }

    partial void OnEndFrameChanged(string value)
    {
        OnPropertyChanged(nameof(FrameSummary));
    }

    partial void OnInspectionChanged(BlendInspectionSnapshot? value)
    {
        OnPropertyChanged(nameof(AvailableCameraNames));
        OnPropertyChanged(nameof(AvailableCollectionNames));
        OnPropertyChanged(nameof(AvailableSceneNames));
        OnPropertyChanged(nameof(AvailableViewLayerNames));
        NotifyResolvedSettingsChanged();
    }

    partial void OnModeChanged(RenderMode value)
    {
        OnPropertyChanged(nameof(HasFrameOverride));
        OnPropertyChanged(nameof(FrameSummary));
        OnPropertyChanged(nameof(IsAnimationMode));
        OnPropertyChanged(nameof(IsFrameRangeMode));
        OnPropertyChanged(nameof(IsSingleFrameMode));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(EffectiveName));
    }

    partial void OnQueueIndexChanged(int value)
    {
        NotifyResolvedSettingsChanged();
    }

    partial void OnOutputFileNameTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputNameOverride));
        NotifyResolvedSettingsChanged();
    }

    partial void OnOutputFormatChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputFormatOverride));
        NotifyResolvedSettingsChanged();
    }

    partial void OnOutputPathTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputPathOverride));
        NotifyResolvedSettingsChanged();
    }

    partial void OnProgressValueChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercentLabel));
    }

    partial void OnLastKnownOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewPathText));
    }

    partial void OnLastErrorSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(LastErrorSummaryText));
    }

    partial void OnPreviewImageSourceChanged(BitmapSource? value)
    {
        OnPropertyChanged(nameof(HasPreviewImage));
    }

    partial void OnSceneNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasSceneOverride));
        NotifyTargetingChanged(sceneSelectionChanged: true);
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
        OnPropertyChanged(nameof(HasViewLayerOverride));
        NotifyTargetingChanged();
    }

    private string BuildInheritedHint(string? inspectedValue, string label)
    {
        return string.IsNullOrWhiteSpace(inspectedValue)
            ? $"Empty = use {label} from blend."
            : $"Empty = from blend: {inspectedValue.Trim()}";
    }

    private void NotifyResolvedSettingsChanged()
    {
        OnPropertyChanged(nameof(BlendFrameSummary));
        OnPropertyChanged(nameof(AvailableCameraNames));
        OnPropertyChanged(nameof(AvailableCollectionNames));
        OnPropertyChanged(nameof(AvailableViewLayerNames));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(CollectionHint));
        OnPropertyChanged(nameof(HasCameraOverride));
        OnPropertyChanged(nameof(HasCollectionOverride));
        OnPropertyChanged(nameof(HasFrameOverride));
        OnPropertyChanged(nameof(HasInspection));
        OnPropertyChanged(nameof(HasOutputFormatOverride));
        OnPropertyChanged(nameof(HasOutputNameOverride));
        OnPropertyChanged(nameof(HasOutputPathOverride));
        OnPropertyChanged(nameof(HasSceneOverride));
        OnPropertyChanged(nameof(HasViewLayerOverride));
        OnPropertyChanged(nameof(InspectionSummary));
        OnPropertyChanged(nameof(OutputDirectoryHint));
        OnPropertyChanged(nameof(OutputFormatHint));
        OnPropertyChanged(nameof(OutputNameHint));
        OnPropertyChanged(nameof(ResolvedCameraName));
        OnPropertyChanged(nameof(ResolvedEndFrameText));
        OnPropertyChanged(nameof(ResolvedOutputDirectory));
        OnPropertyChanged(nameof(ResolvedOutputFormat));
        OnPropertyChanged(nameof(ResolvedOutputName));
        OnPropertyChanged(nameof(ResolvedOutputPattern));
        OnPropertyChanged(nameof(ResolvedSceneName));
        OnPropertyChanged(nameof(ResolvedSingleFrameText));
        OnPropertyChanged(nameof(ResolvedStartFrameText));
        OnPropertyChanged(nameof(ResolvedStepText));
        OnPropertyChanged(nameof(ResolvedViewLayerName));
        OnPropertyChanged(nameof(SceneHint));
        OnPropertyChanged(nameof(UsesOutputFallback));
        OnPropertyChanged(nameof(ViewLayerHint));
        OnPropertyChanged(nameof(FrameSummary));
    }

    private void NotifyTargetingChanged(bool sceneSelectionChanged = false)
    {
        if (sceneSelectionChanged)
        {
            OnPropertyChanged(nameof(AvailableCameraNames));
            OnPropertyChanged(nameof(AvailableCollectionNames));
            OnPropertyChanged(nameof(AvailableViewLayerNames));
        }

        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(OutputDirectoryHint));
        OnPropertyChanged(nameof(OutputNameHint));
        OnPropertyChanged(nameof(ResolvedCameraName));
        OnPropertyChanged(nameof(ResolvedOutputDirectory));
        OnPropertyChanged(nameof(ResolvedOutputName));
        OnPropertyChanged(nameof(ResolvedOutputPattern));
        OnPropertyChanged(nameof(ResolvedSceneName));
        OnPropertyChanged(nameof(ResolvedViewLayerName));
        OnPropertyChanged(nameof(SceneHint));
        OnPropertyChanged(nameof(ViewLayerHint));
    }

    private static string DisplayValue(string? value, string fallback = "Auto")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static RenderJobStatus GetInitialStatus(string? blendFilePath)
    {
        return string.IsNullOrWhiteSpace(blendFilePath)
            ? RenderJobStatus.Pending
            : RenderJobStatus.Ready;
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

    private static string ResolveOverride(string overrideValue, string? inheritedValue)
    {
        return string.IsNullOrWhiteSpace(overrideValue)
            ? inheritedValue?.Trim() ?? string.Empty
            : overrideValue.Trim();
    }

    private static string BuildLifecycleLog(string message)
    {
        return $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}";
    }

    private RenderQueueItemViewModelLike BuildTemplateContext()
    {
        return new RenderQueueItemViewModelLike(
            BlendDirectory,
            BlendFileName,
            OutputPathTemplate,
            OutputFileNameTemplate,
            ResolvedSceneName,
            ResolvedCameraName,
            ResolvedViewLayerName,
            QueueIndex,
            Inspection);
    }
}
