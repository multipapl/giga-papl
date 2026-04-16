using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Models;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Core.Services;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public partial class RenderManagerViewModel : ObservableObject
{
    private static readonly HashSet<string> QueueAggregatePropertyNames =
    [
        nameof(RenderQueueItemViewModel.IsEnabled),
        nameof(RenderQueueItemViewModel.Status),
        nameof(RenderQueueItemViewModel.ProgressValue),
        nameof(RenderQueueItemViewModel.ProgressText),
        nameof(RenderQueueItemViewModel.EtaText),
        nameof(RenderQueueItemViewModel.ElapsedText),
        nameof(RenderQueueItemViewModel.BlendFilePath),
        nameof(RenderQueueItemViewModel.LastKnownOutputPath),
    ];

    private readonly BlendInspectionService _blendInspectionService;
    private readonly RenderCommandBuilder _commandBuilder;
    private readonly IFilePickerService _filePickerService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly RenderJobLogWriter _jobLogWriter;
    private readonly RenderManagerPaths _paths;
    private readonly RenderQueueStore _queueStore;
    private readonly RenderPreviewLoader _renderPreviewLoader;
    private readonly RenderManagerSettingsStore _settingsStore;
    private readonly RenderJobValidationService _validationService;
    private readonly GlobalSettingsService _globalSettingsService;
    private readonly SynchronizationContext _uiContext;
    private readonly DispatcherTimer _logFlushTimer;
    private CancellationTokenSource? _renderCts;
    private int _framesCompleted;
    private QueueRunScope _runScope = QueueRunScope.FullQueue;
    private QueueStopMode? _scheduledStopMode;

    public RenderManagerViewModel(
        BlendInspectionService blendInspectionService,
        RenderCommandBuilder commandBuilder,
        RenderPreviewLoader renderPreviewLoader,
        RenderManagerSettingsStore settingsStore,
        RenderQueueStore queueStore,
        RenderJobValidationService validationService,
        RenderJobLogWriter jobLogWriter,
        RenderManagerPaths paths,
        GlobalSettingsService globalSettingsService,
        IFilePickerService filePickerService,
        IFolderPickerService folderPickerService)
    {
        _blendInspectionService = blendInspectionService;
        _commandBuilder = commandBuilder;
        _renderPreviewLoader = renderPreviewLoader;
        _settingsStore = settingsStore;
        _queueStore = queueStore;
        _validationService = validationService;
        _jobLogWriter = jobLogWriter;
        _paths = paths;
        _globalSettingsService = globalSettingsService;
        _filePickerService = filePickerService;
        _folderPickerService = folderPickerService;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _globalSettingsService.Changed += OnGlobalSettingsChanged;

        Jobs.CollectionChanged += OnJobsCollectionChanged;
        _logFlushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _logFlushTimer.Tick += (_, _) => FlushAllJobLogs();
        _logFlushTimer.Start();

        LoadState();
        RefreshComputedState();
        SetStatus("Render Manager ready.", StatusTone.Neutral);
    }

    public ObservableCollection<RenderQueueItemViewModel> Jobs { get; } = [];

    public IReadOnlyList<RenderMode> FrameOverrideModes { get; } =
    [
        RenderMode.FrameRange,
        RenderMode.SingleFrame,
    ];

    public IReadOnlyList<RenderMode> RenderModes { get; } = Enum.GetValues<RenderMode>();

    public bool HasPausedJobs => Jobs.Any(IsPausedJob);

    public bool HasSelectedJob => SelectedJob is not null;

    public double GlobalProgressValue
    {
        get
        {
            var enabledJobs = Jobs.Where(static job => job.IsEnabled).ToList();
            if (enabledJobs.Count == 0)
            {
                return 0;
            }

            var finishedJobs = enabledJobs.Count(job => IsQueueJobFinished(job.Status));
            return (double)finishedJobs / enabledJobs.Count * 100;
        }
    }

    public string GlobalProgressSummary
    {
        get
        {
            var enabledJobCount = Jobs.Count(static job => job.IsEnabled);
            if (enabledJobCount == 0)
            {
                return "No enabled jobs in the queue yet.";
            }

            var finishedJobCount = Jobs.Count(job => job.IsEnabled && IsQueueJobFinished(job.Status));
            return $"{finishedJobCount}/{enabledJobCount} jobs finished | {GlobalProgressValue:0}% queue progress";
        }
    }

    public string CurrentRunStateText
    {
        get
        {
            var stopSuffix = _scheduledStopMode switch
            {
                QueueStopMode.AfterCurrentFrame => " | stop after current frame requested",
                _ => string.Empty,
            };

            if (IsQueueRunning)
            {
                var runningJob = ResolveRunningJob();
                return runningJob is null
                    ? $"Queue is running{stopSuffix}."
                    : $"Running {runningJob.EffectiveName}{stopSuffix}";
            }

            if (HasPausedJobs)
            {
                var pausedJob = Jobs.FirstOrDefault(IsPausedJob);
                return pausedJob is null
                    ? "Queue paused."
                    : $"Paused on {pausedJob.EffectiveName}";
            }

            return "Queue idle";
        }
    }

    public string QueueSummary => Jobs.Count == 0
        ? "Queue is empty."
        : $"{Jobs.Count(static job => job.IsEnabled)} enabled | {Jobs.Count(job => job.Status == RenderJobStatus.Failed)} failed | {Jobs.Count(static job => !job.IsEnabled)} disabled";

    public string SelectedJobLogOutput => string.IsNullOrWhiteSpace(SelectedJob?.LogOutput)
        ? "Render logs will appear here when a job runs."
        : SelectedJob.LogOutput;

    public string SelectedJobTitle => SelectedJob?.EffectiveName ?? "Select a queue item";

    public string BlenderHeaderHelper => string.IsNullOrWhiteSpace(GlobalBlenderPath)
        ? "Blender is not configured. Open Settings."
        : $"Blender: {GlobalBlenderPath.Trim()}";

    public string SelectedJobBlenderExecutableInput
    {
        get
        {
            var jobPath = SelectedJob?.BlenderExecutablePath?.Trim();
            return string.IsNullOrWhiteSpace(jobPath)
                ? GlobalBlenderPath.Trim()
                : jobPath;
        }
        set
        {
            if (SelectedJob is null)
            {
                return;
            }

            var trimmedValue = value?.Trim() ?? string.Empty;
            SelectedJob.BlenderExecutablePath = string.Equals(trimmedValue, GlobalBlenderPath.Trim(), StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : trimmedValue;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private string globalBlenderPath = string.Empty;

    [ObservableProperty]
    private bool logsExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentRunStateText))]
    [NotifyCanExecuteChangedFor(nameof(StartQueueCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopQueueCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeQueueCommand))]
    private bool isQueueRunning;

    [ObservableProperty]
    private string lastBlendDirectory = string.Empty;

    [ObservableProperty]
    private string lastBlenderDirectory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedJob))]
    [NotifyPropertyChangedFor(nameof(SelectedJobLogOutput))]
    [NotifyPropertyChangedFor(nameof(SelectedJobTitle))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSelectedBlendCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSelectedBlenderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedCameraCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedFrameOverridesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedOutputNameCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSelectedOutputPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedOutputPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedSceneCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSelectedViewLayerCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(InspectSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedOutputFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedOutputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedLogFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetrySelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleSelectedJobEnabledCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseDefaultBlenderForSelectedJobCommand))]
    private RenderQueueItemViewModel? selectedJob;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private StatusTone statusTone = StatusTone.Neutral;

    public void SaveState()
    {
        var settings = new RenderManagerSettings
        {
            LastBlendDirectory = LastBlendDirectory.Trim(),
            LastBlenderDirectory = LastBlenderDirectory.Trim(),
        };

        var queueState = new RenderQueueState
        {
            Items = Jobs.Select(static job => job.ToModel()).ToList(),
            SelectedJobId = SelectedJob?.Id ?? string.Empty,
        };

        _settingsStore.Save(settings);
        _queueStore.Save(queueState);

        _globalSettingsService.Save(new GlobalSettings
        {
            BlenderExecutablePath = _globalSettingsService.Current.BlenderExecutablePath,
            ThemeOverride = _globalSettingsService.Current.ThemeOverride,
            LogsExpanded = LogsExpanded,
        });
    }

    partial void OnGlobalBlenderPathChanged(string value)
    {
        InspectSelectedJobCommand.NotifyCanExecuteChanged();
        UseDefaultBlenderForSelectedJobCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BlenderHeaderHelper));
        OnPropertyChanged(nameof(SelectedJobBlenderExecutableInput));
    }

    partial void OnSelectedJobChanging(RenderQueueItemViewModel? value)
    {
        if (SelectedJob is not null)
        {
            SelectedJob.PropertyChanged -= OnSelectedJobPropertyChanged;
            SelectedJob.IsSelected = false;
        }
    }

    partial void OnSelectedJobChanged(RenderQueueItemViewModel? value)
    {
        if (value is not null)
        {
            value.IsSelected = true;
            value.PropertyChanged += OnSelectedJobPropertyChanged;
            EnsureJobPreviewLoaded(value);
            if (!value.HasInspection)
            {
                _ = TryInspectJobAsync(value, null);
            }
        }

        RefreshComputedState();
    }

    [RelayCommand]
    private void AddBlend()
    {
        var selectedPaths = _filePickerService.PickFiles(
            "Blend files|*.blend|All files|*.*",
            ResolveInitialBlendDirectory(),
            "Choose .blend files");

        if (selectedPaths.Count == 0)
        {
            return;
        }

        RenderQueueItemViewModel? lastAddedJob = null;
        foreach (var selectedPath in selectedPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            LastBlendDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
            var job = RenderQueueItemViewModel.CreateNew(selectedPath);
            Jobs.Add(job);
            lastAddedJob = job;
            _ = TryInspectJobAsync(job, null);
        }

        if (lastAddedJob is not null)
        {
            SelectedJob = lastAddedJob;
            SetStatus($"Added {selectedPaths.Count} blend file(s) to the queue.", StatusTone.Success);
        }
    }

    [RelayCommand]
    private async Task BrowseDefaultBlender()
    {
        var selectedPath = _filePickerService.PickFile(
            "Executable files|*.exe|All files|*.*",
            ResolveInitialBlenderDirectory(),
            "Choose Blender executable");

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        _globalSettingsService.Save(new GlobalSettings
        {
            BlenderExecutablePath = selectedPath,
            ThemeOverride = _globalSettingsService.Current.ThemeOverride,
            LogsExpanded = LogsExpanded,
        });
        GlobalBlenderPath = selectedPath;
        LastBlenderDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        SetStatus("Updated the global Blender executable.", StatusTone.Success);

        if (SelectedJob is not null)
        {
            await TryInspectJobAsync(SelectedJob, "Loaded blend defaults.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private async Task BrowseSelectedBlend()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var selectedPath = _filePickerService.PickFile(
            "Blend files|*.blend|All files|*.*",
            ResolveJobBlendDirectory(SelectedJob),
            "Choose a .blend file");

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        SelectedJob.BlendFilePath = selectedPath;

        LastBlendDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        SetStatus($"Updated source blend for {SelectedJob.EffectiveName}.", StatusTone.Success);
        await TryInspectJobAsync(SelectedJob, "Reloaded blend defaults.");
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void BrowseSelectedBlender()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var selectedPath = _filePickerService.PickFile(
            "Executable files|*.exe|All files|*.*",
            ResolveInitialBlenderDirectory(),
            "Choose Blender executable override");

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        SelectedJob.BlenderExecutablePath = selectedPath;
        LastBlenderDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        SetStatus($"Updated Blender override for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void BrowseSelectedOutputPath()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var selectedPath = _folderPickerService.PickFolder(
            ResolveJobOutputDirectory(SelectedJob),
            "Choose output folder");

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        SelectedJob.Output.OutputPathTemplate = selectedPath;
        SetStatus($"Updated output path for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedOutputPath()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Output.OutputPathTemplate = string.Empty;
        SetStatus($"Restored Blender output path for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedOutputName()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Output.OutputFileNameTemplate = string.Empty;
        SetStatus($"Restored Blender render name for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedFrameOverrides()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.HasFrameOverride = false;
        SetStatus($"Restored blend frame range for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedScene()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Targeting.SceneSelection = JobTargetingViewModel.BlenderDefaultSelection;
        SetStatus($"Restored blend scene for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedCamera()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Targeting.CameraSelection = JobTargetingViewModel.BlenderDefaultSelection;
        SetStatus($"Restored blend camera for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ClearSelectedViewLayer()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Targeting.ViewLayerSelection = JobTargetingViewModel.BlenderDefaultSelection;
        SetStatus($"Restored blend view layer for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void DuplicateSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var index = Jobs.IndexOf(SelectedJob);
        var duplicate = SelectedJob.CreateDuplicate();
        Jobs.Insert(index + 1, duplicate);
        SelectedJob = duplicate;
        SetStatus($"Duplicated {duplicate.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanResetSelectedJob))]
    private void ResetSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.ResetRuntimeState();
        SelectedJob.Status = RenderJobStatus.Ready;
        SetStatus($"Reset {SelectedJob.EffectiveName}.", StatusTone.Success);
        RefreshComputedState();
    }

    [RelayCommand(CanExecute = nameof(CanRetrySelectedJob))]
    private void RetrySelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.ResetRuntimeState("Retry requested.");
        SelectedJob.Status = RenderJobStatus.Ready;
        SetStatus($"{SelectedJob.EffectiveName} is ready to retry.", StatusTone.Success);
        RefreshComputedState();
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void RemoveSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var removedJob = SelectedJob;
        var index = Jobs.IndexOf(removedJob);
        var removedName = removedJob.EffectiveName;

        if (removedJob.Status == RenderJobStatus.Rendering)
        {
            _renderCts?.Cancel();
            IsQueueRunning = false;
        }

        Jobs.Remove(removedJob);
        SelectedJob = Jobs.Count == 0 ? null : Jobs[Math.Clamp(index, 0, Jobs.Count - 1)];
        SetStatus($"Removed {removedName} from the queue.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedJob))]
    private void ToggleSelectedJobEnabled()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.IsEnabled = !SelectedJob.IsEnabled;
        var stateText = SelectedJob.IsEnabled ? "enabled" : "disabled";
        SetStatus($"{SelectedJob.EffectiveName} is now {stateText}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanStartQueue))]
    private void StartQueue()
    {
        StartRun(QueueRunScope.FullQueue, resumePausedJob: false);
    }

    [RelayCommand(CanExecute = nameof(CanStopQueue))]
    private void StopQueue()
    {
        ScheduleStop(QueueStopMode.AfterCurrentFrame, "Queue will stop after the current frame.");
    }

    [RelayCommand(CanExecute = nameof(CanResumeQueue))]
    private void ResumeQueue()
    {
        StartRun(QueueRunScope.FullQueue, resumePausedJob: true);
    }

    [RelayCommand(CanExecute = nameof(CanInspectSelectedJob))]
    private async Task InspectSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        await TryInspectJobAsync(SelectedJob, "Reloaded blend defaults.");
    }

    [RelayCommand(CanExecute = nameof(CanUseDefaultBlenderForSelectedJob))]
    private void UseDefaultBlenderForSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.BlenderExecutablePath = string.Empty;
        SetStatus($"Cleared Blender override for {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedOutputFolder))]
    private void OpenSelectedOutputFolder()
    {
        if (SelectedJob is null)
        {
            return;
        }

        OpenOutputFolder(SelectedJob);
    }

    [RelayCommand]
    private void OpenJobOutputFolder(RenderQueueItemViewModel? job)
    {
        if (job is null)
        {
            return;
        }

        OpenOutputFolder(job);
    }

    private void OpenOutputFolder(RenderQueueItemViewModel job)
    {
        var directory = !string.IsNullOrWhiteSpace(job.LastKnownOutputPath) && File.Exists(job.LastKnownOutputPath)
            ? Path.GetDirectoryName(job.LastKnownOutputPath)
            : job.ResolvedOutputDirectory;

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            SetStatus("Output folder does not exist yet.", StatusTone.Error);
            return;
        }

        OpenPath(directory);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedOutputFile))]
    private void OpenSelectedOutputFile()
    {
        if (SelectedJob is null || string.IsNullOrWhiteSpace(SelectedJob.LastKnownOutputPath))
        {
            return;
        }

        if (!File.Exists(SelectedJob.LastKnownOutputPath))
        {
            SetStatus("Latest rendered file was not found on disk.", StatusTone.Error);
            return;
        }

        OpenPath(SelectedJob.LastKnownOutputPath);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedLogFile))]
    private void OpenSelectedLogFile()
    {
        if (SelectedJob is null || string.IsNullOrWhiteSpace(SelectedJob.LastLogFilePath))
        {
            return;
        }

        if (!File.Exists(SelectedJob.LastLogFilePath))
        {
            SetStatus("Log file does not exist yet.", StatusTone.Error);
            return;
        }

        OpenPath(SelectedJob.LastLogFilePath);
    }

    private void StartRun(QueueRunScope scope, bool resumePausedJob)
    {
        var job = ResolveQueueJobToStart(scope, resumePausedJob);
        if (job is null)
        {
            SetStatus(
                resumePausedJob
                    ? "There is no paused job to resume."
                    : "Add at least one enabled job before starting the queue.",
                StatusTone.Error);
            return;
        }

        _runScope = scope;
        _scheduledStopMode = null;

        var isResume = resumePausedJob && IsPausedJob(job);
        var resumePlan = isResume ? RenderResumePlanner.Create(job) : default;

        if (!isResume)
        {
            PrepareJobForFreshRun(job);
        }

        PrepareJobForExecution(job, isResume, resumePlan);

        IsQueueRunning = true;
        RefreshComputedState();
        SetStatus(
            isResume
                ? $"Resumed {job.EffectiveName}."
                : $"Started the queue with {job.EffectiveName}.",
            StatusTone.Success);

        _renderCts = new CancellationTokenSource();
        _ = RunQueueAsync(_renderCts.Token, resumePlan);
    }

    private void PrepareJobForFreshRun(RenderQueueItemViewModel job)
    {
        job.ResetRuntimeState("Queue item prepared for render.");
        job.Status = job.HasInspection ? RenderJobStatus.Ready : RenderJobStatus.Pending;
    }

    private void PrepareJobForExecution(RenderQueueItemViewModel job, bool isResume, RenderResumePlan resumePlan)
    {
        job.Status = RenderJobStatus.Rendering;
        job.ElapsedText = string.Empty;
        job.EtaText = string.Empty;
        job.LastStartedUtc = DateTimeOffset.Now;
        job.LastCompletedUtc = null;
        job.LastLogFilePath = _paths.CreateJobLogPath(job.Id);
        job.ProgressText = isResume
            ? BuildResumeProgressText(job, resumePlan)
            : "Starting...";

        AppendLog(job, isResume ? BuildResumeLogMessage(resumePlan) : "Starting render...");
    }

    private void RequestImmediateStop()
    {
        _scheduledStopMode = null;
        if (ResolveRunningJob() is { } runningJob)
        {
            runningJob.Status = RenderJobStatus.Stopping;
        }

        _renderCts?.Cancel();
        SetStatus("Stopping... waiting for Blender to exit.", StatusTone.Neutral);
        RefreshComputedState();
    }

    private void ScheduleStop(QueueStopMode stopMode, string statusMessage)
    {
        if (!IsQueueRunning)
        {
            return;
        }

        _scheduledStopMode = stopMode;
        if (ResolveRunningJob() is { } runningJob)
        {
            runningJob.Status = RenderJobStatus.Stopping;
        }

        SetStatus(statusMessage, StatusTone.Neutral);
        RefreshComputedState();
    }

    private async Task RunQueueAsync(CancellationToken ct, RenderResumePlan firstJobResumePlan)
    {
        var nextLaunchUsesResumePlan = true;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var job = ResolveRunningJob();
                if (job is null)
                {
                    break;
                }

                var resumePlan = nextLaunchUsesResumePlan ? firstJobResumePlan : default;
                nextLaunchUsesResumePlan = false;
                await LaunchJobAsync(job, ct, resumePlan);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var nextJob = ResolveNextQueueJob();
                if (nextJob is null)
                {
                    break;
                }

                SelectedJob = nextJob;
                PrepareJobForFreshRun(nextJob);
                PrepareJobForExecution(nextJob, isResume: false, default);
                RefreshComputedState();
            }
        }
        catch (OperationCanceledException)
        {
            // expected when the user stops the queue
        }
        catch (Exception ex)
        {
            if (ResolveRunningJob() is { } failedJob)
            {
                failedJob.Status = RenderJobStatus.Failed;
                failedJob.ProgressText = "Error";
                failedJob.LastErrorSummary = ex.Message;
                AppendLog(failedJob, $"Unexpected error: {ex.Message}");
            }

            SetStatus($"Queue error: {ex.Message}", StatusTone.Error);
        }
        finally
        {
            FinalizeRunState(ct.IsCancellationRequested);
        }
    }

    private async Task LaunchJobAsync(RenderQueueItemViewModel job, CancellationToken ct, RenderResumePlan resumePlan)
    {
        if (!job.HasInspection)
        {
            await TryInspectJobAsync(job, null);
        }

        var validation = _validationService.Validate(job, GlobalBlenderPath);
        if (!validation.IsValid)
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Validation failed";
            job.EtaText = string.Empty;
            job.LastErrorSummary = validation.Errors[0];
            foreach (var error in validation.Errors)
            {
                AppendLog(job, error);
            }

            FlushJobLog(job);
            SetStatus($"{job.EffectiveName} has validation errors.", StatusTone.Error);
            RefreshComputedState();
            return;
        }

        var plan = _commandBuilder.Build(job, GlobalBlenderPath, resumePlan);
        if (string.IsNullOrWhiteSpace(plan.ExecutablePath) || !File.Exists(plan.ExecutablePath))
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Blender not found";
            job.LastErrorSummary = "Blender executable was not found.";
            AppendLog(job, $"Blender executable not found at: {plan.ExecutablePath}");
            FlushJobLog(job);
            return;
        }

        if (string.IsNullOrWhiteSpace(job.BlendFilePath) || !File.Exists(job.BlendFilePath.Trim()))
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Blend file not found";
            job.LastErrorSummary = "Blend file was not found.";
            AppendLog(job, $"Blend file not found at: {job.BlendFilePath}");
            FlushJobLog(job);
            return;
        }

        try
        {
            if (!Directory.Exists(plan.OutputDirectory))
            {
                Directory.CreateDirectory(plan.OutputDirectory);
                AppendLog(job, $"Created output directory: {plan.OutputDirectory}");
            }
        }
        catch (Exception ex)
        {
            AppendLog(job, $"Warning: could not create output directory: {ex.Message}");
        }

        if (plan.UsesBlendOutputFallback)
        {
            AppendLog(job, $"Blend output path is empty or default. Using fallback: {job.ResolvedOutputPattern}");
        }

        AppendLog(job, $"> {plan.ArgumentsDisplayText}");

        using var process = new Process { StartInfo = plan.CreateStartInfo() };
        var progressContext = new JobProgressContext(ComputeTotalFrames(job));
        _framesCompleted = resumePlan.CompletedFrameCount;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Failed to start";
            job.LastErrorSummary = ex.Message;
            AppendLog(job, $"Failed to start Blender: {ex.Message}");
            FlushJobLog(job);
            return;
        }

        var stdoutTask = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
                {
                    var progress = BlenderOutputParser.TryParse(line);
                    PostToUi(() => HandleProcessOutputLine(job, line, progress, progressContext, stopwatch));
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }, CancellationToken.None);

        var stderrTask = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardError.ReadLineAsync(ct) is { } line)
                {
                    var progress = BlenderOutputParser.TryParse(line);
                    PostToUi(() => HandleProcessOutputLine(job, line, progress, progressContext, stopwatch));
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }, CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch
                {
                    // best effort
                }
            }

            throw;
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // stream draining timeout is fine
        }

        stopwatch.Stop();
        job.ElapsedText = FormatElapsed(stopwatch.Elapsed);
        job.LastCompletedUtc = DateTimeOffset.Now;
        job.Status = process.ExitCode == 0 ? RenderJobStatus.Completed : RenderJobStatus.Failed;
        job.ProgressValue = process.ExitCode == 0 ? 100 : job.ProgressValue;
        job.ProgressText = process.ExitCode == 0 ? "Completed" : $"Failed (exit {process.ExitCode})";
        job.EtaText = string.Empty;
        job.LastErrorSummary = process.ExitCode == 0
            ? string.Empty
            : $"Blender exit code: {process.ExitCode}";

        AppendLog(
            job,
            process.ExitCode == 0
                ? $"Render completed in {job.ElapsedText}."
                : $"Blender exited with code {process.ExitCode}.");
        FlushJobLog(job);

        SetStatus(
            process.ExitCode == 0
                ? $"{job.EffectiveName} completed."
                : $"{job.EffectiveName} failed.",
            process.ExitCode == 0 ? StatusTone.Success : StatusTone.Error);

        RefreshComputedState();
    }

    private void HandleProcessOutputLine(
        RenderQueueItemViewModel job,
        string line,
        BlenderProgress? progress,
        JobProgressContext progressContext,
        Stopwatch stopwatch)
    {
        AppendLogRaw(job, line);

        if (progress is not { } parsed)
        {
            return;
        }

        job.ElapsedText = FormatElapsed(stopwatch.Elapsed);

        if (parsed.AnimationStartFrame != 0 || parsed.AnimationEndFrame != 0)
        {
            progressContext.AnimationStartFrame = parsed.AnimationStartFrame;
            progressContext.TotalFrames = Math.Max(1, parsed.AnimationEndFrame - parsed.AnimationStartFrame + 1);

            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (double)_framesCompleted / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, parsed.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, 0);
            }

            RefreshComputedState();
            return;
        }

        if (parsed.FrameNumber > 0)
        {
            job.LastReportedFrameNumber = parsed.FrameNumber;
        }

        if (parsed.IsFrameFinished)
        {
            var frameRenderSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds - progressContext.ElapsedAtLastCompletedFrameSeconds);
            job.CompletedFrameRenderSeconds += frameRenderSeconds;
            progressContext.ElapsedAtLastCompletedFrameSeconds = stopwatch.Elapsed.TotalSeconds;
            _framesCompleted++;
            job.ResumeCompletedFrameCount = _framesCompleted;
            job.LastCompletedFrameNumber = parsed.FrameNumber;

            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (double)_framesCompleted / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, parsed.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, 0);
            }
            else
            {
                job.ProgressText = $"Frame {parsed.FrameNumber} done ({_framesCompleted} total)";
                job.EtaText = string.Empty;
            }

            if (_scheduledStopMode == QueueStopMode.AfterCurrentFrame)
            {
                _renderCts?.Cancel();
            }
        }
        else if (parsed.SavedPath is not null)
        {
            job.LastKnownOutputPath = parsed.SavedPath;
            if (job.IsSelected)
            {
                _ = UpdateJobPreviewAsync(job, parsed.SavedPath);
            }
        }
        else if (parsed.SampleTotal > 0)
        {
            var sampleFraction = (double)parsed.SampleCurrent / parsed.SampleTotal;
            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (_framesCompleted + sampleFraction) / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, parsed.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, sampleFraction);
            }
            else
            {
                job.ProgressValue = sampleFraction * 100;
                job.ProgressText = parsed.FrameNumber > 0
                    ? $"Frame {parsed.FrameNumber}"
                    : $"Sample {parsed.SampleCurrent}/{parsed.SampleTotal}";
                job.EtaText = string.Empty;
            }
        }
        else if (parsed.FrameNumber > 0)
        {
            job.ProgressText = BuildOverallProgressText(job, progressContext, parsed.FrameNumber);
            job.EtaText = BuildOverallEtaText(job, progressContext, 0);
        }

        RefreshComputedState();
    }

    private void FinalizeRunState(bool wasCanceled)
    {
        if (wasCanceled)
        {
            if (ResolveRunningJob() is { } stoppedJob)
            {
                stoppedJob.Status = RenderJobStatus.Canceled;
                stoppedJob.ProgressText = $"Stopped at {stoppedJob.ProgressPercentLabel}";
                AppendLog(stoppedJob, "Render stopped by user.");
                FlushJobLog(stoppedJob);
            }

            SetStatus(
                _scheduledStopMode == QueueStopMode.AfterCurrentFrame
                    ? "Queue stopped after the current frame."
                    : "Queue stopped.",
                StatusTone.Neutral);
        }
        else if (_runScope == QueueRunScope.FullQueue)
        {
            var anyCompleted = Jobs.Any(job => job.Status == RenderJobStatus.Completed);
            if (anyCompleted)
            {
                SetStatus("All queue jobs completed.", StatusTone.Success);
            }
        }

        _scheduledStopMode = null;
        IsQueueRunning = false;
        RefreshComputedState();
    }

    private async Task TryInspectJobAsync(RenderQueueItemViewModel job, string? successStatusMessage)
    {
        if (string.IsNullOrWhiteSpace(job.BlendFilePath) || !File.Exists(job.BlendFilePath.Trim()))
        {
            return;
        }

        var blenderPath = ResolveBlenderPath(job);
        if (string.IsNullOrWhiteSpace(blenderPath) || !File.Exists(blenderPath))
        {
            if (!string.IsNullOrWhiteSpace(successStatusMessage))
            {
                SetStatus("Set Blender executable to inspect blend defaults.", StatusTone.Neutral);
            }

            return;
        }

        var previousStatus = job.Status;
        var canShowInspectingStatus = previousStatus != RenderJobStatus.Rendering;
        var inspectionToken = job.BeginInspection();

        try
        {
            if (canShowInspectingStatus)
            {
                job.Status = RenderJobStatus.Inspecting;
                RefreshComputedState();
            }

            var inspection = await _blendInspectionService.InspectAsync(blenderPath, job.BlendFilePath.Trim(), inspectionToken);
            job.ApplyInspection(inspection);

            if (canShowInspectingStatus)
            {
                job.Status = RenderJobStatus.Ready;
            }

            AppendLog(
                job,
                $"Loaded blend defaults | Scene: {DisplayOrPlaceholder(inspection.SceneName, "n/a")} | Camera: {DisplayOrPlaceholder(inspection.CameraName, "n/a")} | View Layer: {DisplayOrPlaceholder(inspection.ViewLayerName, "n/a")} | Format: {DisplayOrPlaceholder(inspection.OutputFormat, "n/a")}");

            if (!string.IsNullOrWhiteSpace(successStatusMessage))
            {
                SetStatus(successStatusMessage, StatusTone.Success);
            }
        }
        catch (OperationCanceledException)
        {
            if (job.InspectionState == InspectionState.Inspecting)
            {
                job.InspectionState = job.HasInspection ? InspectionState.Ready : InspectionState.NotInspected;
            }
        }
        catch (Exception ex)
        {
            job.InspectionState = InspectionState.Failed;
            if (canShowInspectingStatus)
            {
                job.Status = previousStatus is RenderJobStatus.Inspecting ? RenderJobStatus.Ready : previousStatus;
            }

            AppendLog(job, $"Blend inspection failed: {ex.Message}");
            SetStatus($"Blend inspection failed for {job.EffectiveName}.", StatusTone.Error);
        }
        finally
        {
            RefreshComputedState();
        }
    }

    private void AppendLog(RenderQueueItemViewModel job, string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        job.AppendBufferedLogLine(line);
        _jobLogWriter.AppendLine(job.LastLogFilePath, line);
    }

    private void AppendLogRaw(RenderQueueItemViewModel job, string line)
    {
        job.AppendBufferedLogLine(line);
        _jobLogWriter.AppendLine(job.LastLogFilePath, line);
    }

    private void FlushAllJobLogs()
    {
        foreach (var job in Jobs)
        {
            FlushJobLog(job);
        }
    }

    private void FlushJobLog(RenderQueueItemViewModel job)
    {
        if (!job.FlushLogBuffer())
        {
            return;
        }

        if (job == SelectedJob)
        {
            OnPropertyChanged(nameof(SelectedJobLogOutput));
        }
    }

    private void EnsureJobPreviewLoaded(RenderQueueItemViewModel job)
    {
        if (job.IsSelected && !job.HasPreviewImage && !string.IsNullOrWhiteSpace(job.LastKnownOutputPath))
        {
            _ = UpdateJobPreviewAsync(job, job.LastKnownOutputPath);
        }
    }

    private async Task UpdateJobPreviewAsync(RenderQueueItemViewModel job, string outputPath)
    {
        var normalizedPath = outputPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            !job.CanDecodePreviewNow(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2)))
        {
            return;
        }

        job.PreviewStatusText = $"Loading preview from {Path.GetFileName(normalizedPath)}...";

        RenderPreviewLoadResult previewResult;
        try
        {
            previewResult = await _renderPreviewLoader.LoadAsync(normalizedPath);
        }
        catch (Exception ex)
        {
            previewResult = new RenderPreviewLoadResult(null, $"Preview unavailable: {ex.Message}");
        }

        PostToUi(() =>
        {
            if (!string.Equals(job.LastKnownOutputPath.Trim(), normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            job.PreviewImageSource = previewResult.ImageSource;
            job.PreviewStatusText = previewResult.StatusText;
        });
    }

    private void PostToUi(Action action)
    {
        _uiContext.Post(_ => action(), null);
    }

    private static int ComputeTotalFrames(RenderQueueItemViewModel job)
    {
        return job.Mode switch
        {
            RenderMode.SingleFrame => 1,
            RenderMode.FrameRange when
                int.TryParse(job.ResolvedStartFrameText, out var start) &&
                int.TryParse(job.ResolvedEndFrameText, out var end) =>
                Math.Max(1, (end - start) / (int.TryParse(job.ResolvedStepText, out var step) && step > 0 ? step : 1) + 1),
            _ => 0,
        };
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");
    }

    private static string BuildResumeLogMessage(RenderResumePlan resumePlan)
    {
        if (!resumePlan.HasResumeStartFrame)
        {
            return "Resuming render...";
        }

        return resumePlan.IsPartialFrameResume
            ? $"Resuming render from frame {resumePlan.ResumeStartFrame}."
            : $"Resuming render at frame {resumePlan.ResumeStartFrame}.";
    }

    private static string BuildResumeProgressText(RenderQueueItemViewModel job, RenderResumePlan resumePlan)
    {
        if (!resumePlan.HasResumeStartFrame)
        {
            return string.IsNullOrWhiteSpace(job.ProgressText) ? "Resuming..." : job.ProgressText;
        }

        return resumePlan.IsPartialFrameResume
            ? $"Resuming frame {resumePlan.ResumeStartFrame}..."
            : $"Continuing at frame {resumePlan.ResumeStartFrame}...";
    }

    private static string BuildOverallProgressText(
        RenderQueueItemViewModel job,
        JobProgressContext progressContext,
        int frameNumber)
    {
        if (progressContext.TotalFrames <= 0)
        {
            return frameNumber > 0 ? $"Frame {frameNumber}" : "Rendering";
        }

        var currentFrameIndex = ResolveCurrentFrameIndex(job, progressContext, frameNumber);
        var clampedFrameIndex = Math.Clamp(currentFrameIndex, 1, progressContext.TotalFrames);
        return $"Frame {clampedFrameIndex}/{progressContext.TotalFrames}";
    }

    private static int ResolveCurrentFrameIndex(
        RenderQueueItemViewModel job,
        JobProgressContext progressContext,
        int frameNumber)
    {
        if (frameNumber <= 0)
        {
            return Math.Min(progressContext.TotalFrames, Math.Max(1, job.ResumeCompletedFrameCount + 1));
        }

        return job.Mode switch
        {
            RenderMode.FrameRange => ResolveFrameRangeIndex(job, frameNumber),
            RenderMode.Animation when progressContext.AnimationStartFrame != 0 =>
                frameNumber - progressContext.AnimationStartFrame + 1,
            _ => frameNumber,
        };
    }

    private static int ResolveFrameRangeIndex(RenderQueueItemViewModel job, int frameNumber)
    {
        var startFrame = int.TryParse(job.ResolvedStartFrameText, out var parsedStartFrame) ? parsedStartFrame : frameNumber;
        var step = int.TryParse(job.ResolvedStepText, out var parsedStep) && parsedStep > 0 ? parsedStep : 1;
        return ((frameNumber - startFrame) / step) + 1;
    }

    private static string BuildOverallEtaText(
        RenderQueueItemViewModel job,
        JobProgressContext progressContext,
        double currentFrameFraction)
    {
        return RenderEtaCalculator.BuildEtaText(
            progressContext.TotalFrames,
            job.ResumeCompletedFrameCount,
            job.CompletedFrameRenderSeconds,
            currentFrameFraction);
    }

    private static bool IsQueueJobFinished(RenderJobStatus status)
    {
        return status is RenderJobStatus.Completed or RenderJobStatus.Failed or RenderJobStatus.Skipped;
    }

    private static bool IsStartableQueueStatus(RenderJobStatus status)
    {
        return status is RenderJobStatus.Pending or RenderJobStatus.Ready or RenderJobStatus.Inspecting;
    }

    private static bool IsPausedJob(RenderQueueItemViewModel job)
    {
        return job.IsEnabled && job.Status is RenderJobStatus.Canceled;
    }

    private void LoadState()
    {
        var settings = _settingsStore.Load();
        GlobalBlenderPath = _globalSettingsService.Current.BlenderExecutablePath ?? string.Empty;
        LogsExpanded = _globalSettingsService.Current.LogsExpanded;
        LastBlendDirectory = settings.LastBlendDirectory ?? string.Empty;
        LastBlenderDirectory = settings.LastBlenderDirectory ?? string.Empty;

        var queueState = _queueStore.Load();
        Jobs.Clear();
        foreach (var item in queueState.Items)
        {
            Jobs.Add(RenderQueueItemViewModel.FromModel(item));
        }

        UpdateQueueIndices();
        SelectedJob = Jobs.FirstOrDefault(job => job.Id == queueState.SelectedJobId) ?? Jobs.FirstOrDefault();
    }

    private void OnGlobalSettingsChanged(object? sender, EventArgs e)
    {
        GlobalBlenderPath = _globalSettingsService.Current.BlenderExecutablePath ?? string.Empty;
        LogsExpanded = _globalSettingsService.Current.LogsExpanded;
        OnPropertyChanged(nameof(BlenderHeaderHelper));
    }

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<RenderQueueItemViewModel>())
            {
                item.PropertyChanged -= OnQueueItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RenderQueueItemViewModel>())
            {
                item.PropertyChanged += OnQueueItemPropertyChanged;
            }
        }

        UpdateQueueIndices();
        RefreshComputedState();
    }

    private void UpdateQueueIndices()
    {
        for (var index = 0; index < Jobs.Count; index++)
        {
            Jobs[index].QueueIndex = index + 1;
        }
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RenderQueueItemViewModel job)
        {
            return;
        }

        if (job == SelectedJob)
        {
            OnPropertyChanged(nameof(SelectedJobLogOutput));
            OnPropertyChanged(nameof(SelectedJobTitle));
            OpenSelectedOutputFileCommand.NotifyCanExecuteChanged();
            OpenSelectedOutputFolderCommand.NotifyCanExecuteChanged();
            OpenSelectedLogFileCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is null || QueueAggregatePropertyNames.Contains(e.PropertyName))
        {
            RefreshComputedState();
        }
    }

    private void OnSelectedJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedJobLogOutput));
        OnPropertyChanged(nameof(SelectedJobTitle));

        if (e.PropertyName is nameof(RenderQueueItemViewModel.LastKnownOutputPath)
            or nameof(RenderQueueItemViewModel.LastLogFilePath)
            or nameof(RenderQueueItemViewModel.BlendFilePath))
        {
            OpenSelectedOutputFileCommand.NotifyCanExecuteChanged();
            OpenSelectedOutputFolderCommand.NotifyCanExecuteChanged();
            OpenSelectedLogFileCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is nameof(RenderQueueItemViewModel.IsEnabled) or nameof(RenderQueueItemViewModel.Status))
        {
            RefreshComputedState();
        }

        if (e.PropertyName is nameof(RenderQueueItemViewModel.BlenderExecutablePath))
        {
            OnPropertyChanged(nameof(SelectedJobBlenderExecutableInput));
        }
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(CurrentRunStateText));
        OnPropertyChanged(nameof(GlobalProgressSummary));
        OnPropertyChanged(nameof(GlobalProgressValue));
        OnPropertyChanged(nameof(HasPausedJobs));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(SelectedJobLogOutput));
        OnPropertyChanged(nameof(SelectedJobTitle));
        OnPropertyChanged(nameof(SelectedJobBlenderExecutableInput));

        BrowseSelectedBlendCommand.NotifyCanExecuteChanged();
        BrowseSelectedBlenderCommand.NotifyCanExecuteChanged();
        ClearSelectedCameraCommand.NotifyCanExecuteChanged();
        ClearSelectedFrameOverridesCommand.NotifyCanExecuteChanged();
        ClearSelectedOutputNameCommand.NotifyCanExecuteChanged();
        BrowseSelectedOutputPathCommand.NotifyCanExecuteChanged();
        ClearSelectedOutputPathCommand.NotifyCanExecuteChanged();
        ClearSelectedSceneCommand.NotifyCanExecuteChanged();
        ClearSelectedViewLayerCommand.NotifyCanExecuteChanged();
        DuplicateSelectedJobCommand.NotifyCanExecuteChanged();
        InspectSelectedJobCommand.NotifyCanExecuteChanged();
        OpenSelectedOutputFileCommand.NotifyCanExecuteChanged();
        OpenSelectedOutputFolderCommand.NotifyCanExecuteChanged();
        OpenSelectedLogFileCommand.NotifyCanExecuteChanged();
        RemoveSelectedJobCommand.NotifyCanExecuteChanged();
        ResetSelectedJobCommand.NotifyCanExecuteChanged();
        ResumeQueueCommand.NotifyCanExecuteChanged();
        RetrySelectedJobCommand.NotifyCanExecuteChanged();
        StartQueueCommand.NotifyCanExecuteChanged();
        StopQueueCommand.NotifyCanExecuteChanged();
        UseDefaultBlenderForSelectedJobCommand.NotifyCanExecuteChanged();
    }

    private string ResolveInitialBlendDirectory()
    {
        return Directory.Exists(LastBlendDirectory)
            ? LastBlendDirectory
            : string.Empty;
    }

    private string ResolveInitialBlenderDirectory()
    {
        if (Directory.Exists(LastBlenderDirectory))
        {
            return LastBlenderDirectory;
        }

        var currentDefaultDirectory = Path.GetDirectoryName(GlobalBlenderPath);
        return Directory.Exists(currentDefaultDirectory) ? currentDefaultDirectory : string.Empty;
    }

    private string ResolveJobBlendDirectory(RenderQueueItemViewModel job)
    {
        var jobDirectory = Path.GetDirectoryName(job.BlendFilePath);
        if (Directory.Exists(jobDirectory))
        {
            return jobDirectory;
        }

        return ResolveInitialBlendDirectory();
    }

    private string ResolveJobOutputDirectory(RenderQueueItemViewModel job)
    {
        var overridePath = job.Output.OutputPathTemplate?.Trim();
        if (Directory.Exists(overridePath))
        {
            return overridePath;
        }

        var resolvedOutputDirectory = job.ResolvedOutputDirectory?.Trim();
        if (Directory.Exists(resolvedOutputDirectory))
        {
            return resolvedOutputDirectory;
        }

        var blendDirectory = Path.GetDirectoryName(job.BlendFilePath);
        return Directory.Exists(blendDirectory) ? blendDirectory : string.Empty;
    }

    private RenderQueueItemViewModel? ResolveQueueJobToStart(QueueRunScope scope, bool resumePausedJob)
    {
        if (resumePausedJob)
        {
            return Jobs.FirstOrDefault(IsPausedJob);
        }

        return Jobs.FirstOrDefault(job => job.IsEnabled && IsStartableQueueStatus(job.Status));
    }

    private RenderQueueItemViewModel? ResolveNextQueueJob()
    {
        return Jobs.FirstOrDefault(job => job.IsEnabled && IsStartableQueueStatus(job.Status));
    }

    private RenderQueueItemViewModel? ResolveRunningJob()
    {
        return Jobs.FirstOrDefault(job => job.Status is RenderJobStatus.Rendering or RenderJobStatus.Stopping);
    }

    private string ResolveBlenderPath(RenderQueueItemViewModel job)
    {
        return string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? GlobalBlenderPath.Trim()
            : job.BlenderExecutablePath.Trim();
    }

    private bool CanEditSelectedJob()
    {
        return SelectedJob is not null && !IsQueueRunning;
    }

    private bool CanResetSelectedJob()
    {
        return CanEditSelectedJob() && SelectedJob is not null;
    }

    private bool CanRetrySelectedJob()
    {
        return CanEditSelectedJob() && SelectedJob is not null &&
               SelectedJob.Status is RenderJobStatus.Failed or RenderJobStatus.Canceled;
    }

    private bool CanInspectSelectedJob()
    {
        return SelectedJob is not null && !IsQueueRunning;
    }

    private bool CanResumeQueue()
    {
        return !IsQueueRunning && Jobs.Any(IsPausedJob);
    }

    private bool CanStartQueue()
    {
        return !IsQueueRunning && Jobs.Any(job => job.IsEnabled && IsStartableQueueStatus(job.Status));
    }

    private bool CanStopQueue()
    {
        return IsQueueRunning;
    }

    private bool CanUseDefaultBlenderForSelectedJob()
    {
        return CanEditSelectedJob() &&
               SelectedJob is not null &&
               !string.IsNullOrWhiteSpace(SelectedJob.BlenderExecutablePath) &&
               !string.IsNullOrWhiteSpace(GlobalBlenderPath);
    }

    private bool CanOpenSelectedOutputFolder()
    {
        return SelectedJob is not null &&
               (!string.IsNullOrWhiteSpace(SelectedJob.ResolvedOutputDirectory) ||
                !string.IsNullOrWhiteSpace(SelectedJob.LastKnownOutputPath));
    }

    private bool CanOpenSelectedOutputFile()
    {
        return SelectedJob is not null &&
               !string.IsNullOrWhiteSpace(SelectedJob.LastKnownOutputPath) &&
               File.Exists(SelectedJob.LastKnownOutputPath);
    }

    private bool CanOpenSelectedLogFile()
    {
        return SelectedJob is not null &&
               !string.IsNullOrWhiteSpace(SelectedJob.LastLogFilePath) &&
               File.Exists(SelectedJob.LastLogFilePath);
    }

    private static string DisplayOrPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusMessage = message;
        StatusTone = tone;
    }

    private enum QueueRunScope
    {
        FullQueue,
    }

    private sealed class JobProgressContext
    {
        public JobProgressContext(int totalFrames)
        {
            TotalFrames = totalFrames;
        }

        public int AnimationStartFrame { get; set; }

        public double ElapsedAtLastCompletedFrameSeconds { get; set; }

        public int TotalFrames { get; set; }
    }
}
