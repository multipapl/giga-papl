using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public partial class RenderManagerViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> SupportedOutputFormats =
    [
        "PNG",
        "JPEG",
        "OPEN_EXR",
        "OPEN_EXR_MULTILAYER",
        "TIFF",
        "BMP",
        "TARGA",
        "WEBP",
    ];

    private readonly BlendInspectionService _blendInspectionService;
    private readonly IFilePickerService _filePickerService;
    private readonly RenderQueueStore _queueStore;
    private readonly string _runtimeDirectory;
    private readonly RenderManagerSettingsStore _settingsStore;
    private readonly SynchronizationContext _uiContext;
    private CancellationTokenSource? _renderCts;
    private int _framesCompleted;

    public RenderManagerViewModel(
        BlendInspectionService blendInspectionService,
        RenderManagerSettingsStore settingsStore,
        RenderQueueStore queueStore,
        IFilePickerService filePickerService)
    {
        _blendInspectionService = blendInspectionService;
        _settingsStore = settingsStore;
        _queueStore = queueStore;
        _filePickerService = filePickerService;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _runtimeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlenderToolbox",
            "RenderManager",
            "runtime");

        Jobs.CollectionChanged += OnJobsCollectionChanged;

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

    public IReadOnlyList<string> OutputFormats { get; } = SupportedOutputFormats;

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
            if (IsQueueRunning)
            {
                var runningJob = ResolveRunningJob();
                return runningJob is null
                    ? "Queue is running."
                    : $"Running {runningJob.EffectiveName}";
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
        : $"{Jobs.Count(static job => job.IsEnabled)} enabled | {Jobs.Count(static job => !job.IsEnabled)} disabled";

    public string SelectedJobLogOutput => string.IsNullOrWhiteSpace(SelectedJob?.LogOutput)
        ? "Render logs will appear here when a job runs."
        : SelectedJob.LogOutput;

    public string SelectedJobTitle => SelectedJob?.EffectiveName ?? "Select a queue item";

    [ObservableProperty]
    private string defaultBlenderPath = string.Empty;

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
    [NotifyCanExecuteChangedFor(nameof(DuplicateSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(InspectSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedJobDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedJobUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedJobCommand))]
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
            DefaultBlenderPath = DefaultBlenderPath.Trim(),
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
    }

    partial void OnDefaultBlenderPathChanged(string value)
    {
        InspectSelectedJobCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedJobChanging(RenderQueueItemViewModel? value)
    {
        if (SelectedJob is not null)
        {
            SelectedJob.PropertyChanged -= OnSelectedJobPropertyChanged;
        }
    }

    partial void OnSelectedJobChanged(RenderQueueItemViewModel? value)
    {
        if (value is not null)
        {
            value.PropertyChanged += OnSelectedJobPropertyChanged;
            if (!value.HasInspection)
            {
                _ = TryInspectJobAsync(value, null);
            }
        }

        RefreshComputedState();
    }

    [RelayCommand]
    private async Task AddBlend()
    {
        var selectedPath = _filePickerService.PickFile(
            "Blend files|*.blend|All files|*.*",
            ResolveInitialBlendDirectory(),
            "Choose a .blend file");

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        LastBlendDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        var job = RenderQueueItemViewModel.CreateNew(selectedPath);

        Jobs.Add(job);
        SelectedJob = job;
        SetStatus($"Added {job.EffectiveName} to the queue.", StatusTone.Success);
        await TryInspectJobAsync(job, "Loaded blend defaults.");
    }

    [RelayCommand]
    private void AddEmptyJob()
    {
        var job = RenderQueueItemViewModel.CreateNew(string.Empty);

        Jobs.Add(job);
        SelectedJob = job;
        SetStatus("Added an empty queue item. Fill in the blend and render fields in the details panel.", StatusTone.Success);
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

        DefaultBlenderPath = selectedPath;
        LastBlenderDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        SetStatus("Updated the default Blender executable.", StatusTone.Success);

        if (SelectedJob is not null)
        {
            await TryInspectJobAsync(SelectedJob, "Loaded blend defaults.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
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
        if (string.IsNullOrWhiteSpace(SelectedJob.Name) || SelectedJob.Name == "New render job")
        {
            SelectedJob.Name = Path.GetFileNameWithoutExtension(selectedPath);
        }

        LastBlendDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
        SetStatus($"Updated source blend for {SelectedJob.EffectiveName}.", StatusTone.Success);
        await TryInspectJobAsync(SelectedJob, "Reloaded blend defaults.");
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
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
        SetStatus($"Reset {SelectedJob.EffectiveName}.", StatusTone.Success);
        RefreshComputedState();
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedJobDown))]
    private void MoveSelectedJobDown()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var index = Jobs.IndexOf(SelectedJob);
        Jobs.Move(index, index + 1);
        SetStatus($"Moved {SelectedJob.EffectiveName} down in the queue.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedJobUp))]
    private void MoveSelectedJobUp()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var index = Jobs.IndexOf(SelectedJob);
        Jobs.Move(index, index - 1);
        SetStatus($"Moved {SelectedJob.EffectiveName} up in the queue.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
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

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
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
        StartOrResumeQueue(resumePausedJob: false);
    }

    [RelayCommand(CanExecute = nameof(CanStopQueue))]
    private void StopQueue()
    {
        RequestQueueStop();
    }

    [RelayCommand(CanExecute = nameof(CanResumeQueue))]
    private void ResumeQueue()
    {
        StartOrResumeQueue(resumePausedJob: true);
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

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
    private void UseDefaultBlenderForSelectedJob()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.BlenderExecutablePath = DefaultBlenderPath;
        SetStatus($"Applied the default Blender executable to {SelectedJob.EffectiveName}.", StatusTone.Success);
    }

    // Render execution

    private void StartOrResumeQueue(bool resumePausedJob)
    {
        var job = ResolveQueueJobToStart(resumePausedJob);
        if (job is null)
        {
            SetStatus(
                resumePausedJob
                    ? "There is no paused job to resume."
                    : "Add at least one enabled job before starting the queue.",
                StatusTone.Error);
            return;
        }

        var isResume = resumePausedJob && IsPausedJob(job);
        var resumePlan = isResume ? RenderResumePlanner.Create(job) : default;
        SelectedJob = job;
        job.Status = RenderJobStatus.Rendering;
        if (!isResume)
        {
            job.ProgressValue = 0;
            job.CompletedFrameRenderSeconds = 0;
            job.ResumeCompletedFrameCount = 0;
            job.LastReportedFrameNumber = 0;
            job.LastCompletedFrameNumber = 0;
            job.ProgressText = "Starting...";
        }
        else
        {
            job.ProgressText = BuildResumeProgressText(job, resumePlan);
        }

        job.ElapsedText = string.Empty;
        job.EtaText = string.Empty;
        job.LastStartedUtc = DateTimeOffset.Now;
        AppendLog(job, isResume ? BuildResumeLogMessage(resumePlan) : "Starting render...");

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

    private void RequestQueueStop()
    {
        _renderCts?.Cancel();
        SetStatus("Stopping... waiting for Blender to exit.", StatusTone.Neutral);
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

                // Find the next enabled job that can still be rendered.
                var nextJob = Jobs.FirstOrDefault(j =>
                    j.IsEnabled && j.Status is RenderJobStatus.Ready or RenderJobStatus.Pending or RenderJobStatus.Inspecting);

                if (nextJob is null)
                {
                    break;
                }

                nextJob.Status = RenderJobStatus.Rendering;
                nextJob.ProgressValue = 0;
                nextJob.ProgressText = "Starting...";
                nextJob.ElapsedText = string.Empty;
                nextJob.EtaText = string.Empty;
                nextJob.LastStartedUtc = DateTimeOffset.Now;
                SelectedJob = nextJob;
                AppendLog(nextJob, "Starting render...");
                RefreshComputedState();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation; handled below.
        }
        catch (Exception ex)
        {
            var failedJob = ResolveRunningJob();
            if (failedJob is not null)
            {
                failedJob.Status = RenderJobStatus.Failed;
                failedJob.ProgressText = "Error";
                AppendLog(failedJob, $"Unexpected error: {ex.Message}");
            }

            SetStatus($"Queue error: {ex.Message}", StatusTone.Error);
        }
        finally
        {
            // Mark any still-rendering job as canceled if we were stopped
            if (ct.IsCancellationRequested)
            {
                var stoppedJob = ResolveRunningJob();
                if (stoppedJob is not null)
                {
                    stoppedJob.Status = RenderJobStatus.Canceled;
                    stoppedJob.ProgressText = $"Stopped at {stoppedJob.ProgressPercentLabel}";
                    AppendLog(stoppedJob, "Render stopped by user.");
                }

                SetStatus("Queue stopped.", StatusTone.Neutral);
            }
            else
            {
                var anyCompleted = Jobs.Any(j => j.Status == RenderJobStatus.Completed);
                if (anyCompleted)
                {
                    SetStatus("All queue jobs completed.", StatusTone.Success);
                }
            }

            IsQueueRunning = false;
            RefreshComputedState();
        }
    }

    private async Task LaunchJobAsync(RenderQueueItemViewModel job, CancellationToken ct, RenderResumePlan resumePlan)
    {
        if (!job.HasInspection)
        {
            await TryInspectJobAsync(job, null);
        }

        var plan = BuildProcessCommand(job, resumePlan);
        var exePath = plan.ExePath;
        var arguments = plan.Arguments;

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Blender not found";
            AppendLog(job, $"Blender executable not found at: {exePath}");
            return;
        }

        if (string.IsNullOrWhiteSpace(job.BlendFilePath) || !File.Exists(job.BlendFilePath.Trim()))
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = "Blend file not found";
            AppendLog(job, $"Blend file not found at: {job.BlendFilePath}");
            return;
        }

        // Ensure the output directory exists
        try
        {
            var resolvedDir = plan.OutputDirectory;
            if (!Directory.Exists(resolvedDir))
            {
                Directory.CreateDirectory(resolvedDir);
                AppendLog(job, $"Created output directory: {resolvedDir}");
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

        AppendLog(job, $"> \"{exePath}\" {arguments}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = psi };

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
            AppendLog(job, $"Failed to start Blender: {ex.Message}");
            return;
        }

        // Read process output on background threads and marshal UI updates back to the UI context.
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
                catch { /* best effort */ }
            }

            throw;
        }

        // Let stream readers finish draining.
        try { await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { /* timeout is fine, streams may already be closed */ }

        stopwatch.Stop();
        job.ElapsedText = FormatElapsed(stopwatch.Elapsed);
        job.LastCompletedUtc = DateTimeOffset.Now;

        if (process.ExitCode == 0)
        {
            job.Status = RenderJobStatus.Completed;
            job.ProgressValue = 100;
            job.ProgressText = "Completed";
            job.EtaText = string.Empty;
            AppendLog(job, $"Render completed in {job.ElapsedText}.");
            SetStatus($"{job.EffectiveName} completed.", StatusTone.Success);
        }
        else
        {
            job.Status = RenderJobStatus.Failed;
            job.ProgressText = $"Failed (exit {process.ExitCode})";
            job.EtaText = string.Empty;
            job.LastErrorSummary = $"Blender exit code: {process.ExitCode}";
            AppendLog(job, $"Blender exited with code {process.ExitCode}.");
            SetStatus($"{job.EffectiveName} failed.", StatusTone.Error);
        }

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

        if (progress is not { } p)
        {
            return;
        }

        job.ElapsedText = FormatElapsed(stopwatch.Elapsed);

        if (p.AnimationStartFrame != 0 || p.AnimationEndFrame != 0)
        {
            progressContext.AnimationStartFrame = p.AnimationStartFrame;
            progressContext.TotalFrames = Math.Max(1, p.AnimationEndFrame - p.AnimationStartFrame + 1);

            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (double)_framesCompleted / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, p.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, 0);
            }

            RefreshComputedState();
            return;
        }

        if (p.FrameNumber > 0)
        {
            job.LastReportedFrameNumber = p.FrameNumber;
        }

        if (p.IsFrameFinished)
        {
            var frameRenderSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds - progressContext.ElapsedAtLastCompletedFrameSeconds);
            job.CompletedFrameRenderSeconds += frameRenderSeconds;
            progressContext.ElapsedAtLastCompletedFrameSeconds = stopwatch.Elapsed.TotalSeconds;
            _framesCompleted++;
            job.ResumeCompletedFrameCount = _framesCompleted;
            job.LastCompletedFrameNumber = p.FrameNumber;

            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (double)_framesCompleted / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, p.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, 0);
            }
            else
            {
                job.ProgressText = $"Frame {p.FrameNumber} done ({_framesCompleted} total)";
                job.EtaText = string.Empty;
            }
        }
        else if (p.SavedPath is not null)
        {
            job.LastKnownOutputPath = p.SavedPath;
        }
        else if (p.SampleTotal > 0)
        {
            var sampleFraction = (double)p.SampleCurrent / p.SampleTotal;

            if (progressContext.TotalFrames > 0)
            {
                job.ProgressValue = (_framesCompleted + sampleFraction) / progressContext.TotalFrames * 100;
                job.ProgressText = BuildOverallProgressText(job, progressContext, p.FrameNumber);
                job.EtaText = BuildOverallEtaText(job, progressContext, sampleFraction);
            }
            else
            {
                job.ProgressValue = sampleFraction * 100;
                job.ProgressText = p.FrameNumber > 0
                    ? $"Frame {p.FrameNumber}"
                    : $"Sample {p.SampleCurrent}/{p.SampleTotal}";
                job.EtaText = string.Empty;
            }
        }
        else if (p.FrameNumber > 0)
        {
            job.ProgressText = BuildOverallProgressText(job, progressContext, p.FrameNumber);
            job.EtaText = BuildOverallEtaText(job, progressContext, 0);
        }

        RefreshComputedState();
    }

    // Process command building

    private ProcessCommandPlan BuildProcessCommand(RenderQueueItemViewModel job, RenderResumePlan resumePlan)
    {
        var blenderPath = string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? DefaultBlenderPath
            : job.BlenderExecutablePath;

        var args = new List<string>
        {
            "--background",
            Quote(job.BlendFilePath.Trim()),
        };

        if (!string.IsNullOrWhiteSpace(job.SceneName))
        {
            args.Add("--scene");
            args.Add(Quote(job.SceneName.Trim()));
        }

        var overrideScriptPath = CreateOverrideScriptIfNeeded(job);
        if (!string.IsNullOrWhiteSpace(overrideScriptPath))
        {
            args.Add("--python");
            args.Add(Quote(overrideScriptPath));
        }

        switch (job.Mode)
        {
            case RenderMode.Animation:
                if (resumePlan.HasResumeStartFrame)
                {
                    args.Add("-s");
                    args.Add(resumePlan.ResumeStartFrame.ToString());
                }

                args.Add("--render-anim");
                break;

            case RenderMode.FrameRange:
                var rangeStartFrame = resumePlan.HasResumeStartFrame
                    ? resumePlan.ResumeStartFrame.ToString()
                    : ResolveFrameRangeStart(job);
                if (!string.IsNullOrWhiteSpace(rangeStartFrame))
                {
                    args.Add("-s");
                    args.Add(rangeStartFrame);
                }

                var rangeEndFrame = ResolveFrameRangeEnd(job);
                if (!string.IsNullOrWhiteSpace(rangeEndFrame))
                {
                    args.Add("-e");
                    args.Add(rangeEndFrame);
                }

                args.Add("-j");
                args.Add(ResolveFrameStep(job));
                args.Add("-a");
                break;

            case RenderMode.SingleFrame:
                args.Add("--render-frame");
                args.Add(ResolveSingleFrame(job));
                break;
        }

        if (!string.IsNullOrWhiteSpace(job.ExtraArgs))
        {
            args.Add(job.ExtraArgs.Trim());
        }

        return new ProcessCommandPlan(
            blenderPath.Trim(),
            string.Join(" ", args),
            overrideScriptPath,
            job.ResolvedOutputDirectory,
            job.UsesOutputFallback);
    }

    private static int ComputeTotalFrames(RenderQueueItemViewModel job)
    {
        return job.Mode switch
        {
            RenderMode.SingleFrame => 1,
            RenderMode.FrameRange when
                int.TryParse(ResolveFrameRangeStart(job), out var start) &&
                int.TryParse(ResolveFrameRangeEnd(job), out var end) =>
                Math.Max(1, (end - start) / (int.TryParse(ResolveFrameStep(job), out var s) && s > 0 ? s : 1) + 1),
            _ => 0,
        };
    }

    // Logging

    private void AppendLog(RenderQueueItemViewModel job, string message)
    {
        var prefix = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] ";
        job.LogOutput = string.IsNullOrWhiteSpace(job.LogOutput)
            ? $"{prefix}{message}"
            : $"{job.LogOutput}{Environment.NewLine}{prefix}{message}";

        if (job == SelectedJob)
        {
            OnPropertyChanged(nameof(SelectedJobLogOutput));
        }
    }

    private void AppendLogRaw(RenderQueueItemViewModel job, string line)
    {
        job.LogOutput = string.IsNullOrWhiteSpace(job.LogOutput)
            ? line
            : $"{job.LogOutput}{Environment.NewLine}{line}";

        if (job == SelectedJob)
        {
            OnPropertyChanged(nameof(SelectedJobLogOutput));
        }
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

        try
        {
            if (canShowInspectingStatus)
            {
                job.Status = RenderJobStatus.Inspecting;
                RefreshComputedState();
            }

            var inspection = await _blendInspectionService.InspectAsync(blenderPath, job.BlendFilePath.Trim());
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
        catch (Exception ex)
        {
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

    private string CreateOverrideScriptIfNeeded(RenderQueueItemViewModel job)
    {
        if (!ShouldCreateOverrideScript(job))
        {
            return string.Empty;
        }

        Directory.CreateDirectory(_runtimeDirectory);
        var scriptPath = Path.Combine(_runtimeDirectory, $"render_overrides_{job.Id}.py");
        File.WriteAllText(scriptPath, BuildOverrideScript(job), Encoding.UTF8);
        return scriptPath;
    }

    private static string BuildOverrideScript(RenderQueueItemViewModel job)
    {
        var builder = new StringBuilder();
        builder.AppendLine("import bpy");
        builder.AppendLine();
        builder.AppendLine("scene = bpy.context.scene");

        if (!string.IsNullOrWhiteSpace(job.CameraName))
        {
            builder.AppendLine();
            builder.AppendLine($"camera_name = {JsonSerializer.Serialize(job.CameraName.Trim())}");
            builder.AppendLine("camera = bpy.data.objects.get(camera_name)");
            builder.AppendLine("if camera is None or camera.type != 'CAMERA':");
            builder.AppendLine("    raise RuntimeError(f'Camera not found: {camera_name}')");
            builder.AppendLine("scene.camera = camera");
        }

        if (!string.IsNullOrWhiteSpace(job.ViewLayerName))
        {
            builder.AppendLine();
            builder.AppendLine($"view_layer_name = {JsonSerializer.Serialize(job.ViewLayerName.Trim())}");
            builder.AppendLine("view_layer = scene.view_layers.get(view_layer_name)");
            builder.AppendLine("if view_layer is None:");
            builder.AppendLine("    raise RuntimeError(f'View layer not found: {view_layer_name}')");
            builder.AppendLine("for item in scene.view_layers:");
            builder.AppendLine("    item.use = item.name == view_layer_name");
        }

        if (!string.IsNullOrWhiteSpace(job.ResolvedOutputPattern) &&
            (job.UsesOutputFallback ||
             !string.IsNullOrWhiteSpace(job.OutputPathTemplate) ||
             !string.IsNullOrWhiteSpace(job.OutputFileNameTemplate)))
        {
            builder.AppendLine();
            builder.AppendLine($"scene.render.filepath = {JsonSerializer.Serialize(job.ResolvedOutputPattern)}");
        }

        if (!string.IsNullOrWhiteSpace(job.OutputFormat))
        {
            builder.AppendLine();
            builder.AppendLine($"scene.render.image_settings.file_format = {JsonSerializer.Serialize(job.OutputFormat.Trim())}");
        }

        return builder.ToString();
    }

    private static string ResolveBlenderPath(RenderQueueItemViewModel job, string defaultBlenderPath)
    {
        return string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? defaultBlenderPath.Trim()
            : job.BlenderExecutablePath.Trim();
    }

    private string ResolveBlenderPath(RenderQueueItemViewModel job)
    {
        return ResolveBlenderPath(job, DefaultBlenderPath);
    }

    private static string ResolveFrameRangeEnd(RenderQueueItemViewModel job)
    {
        if (!string.IsNullOrWhiteSpace(job.EndFrame))
        {
            return job.EndFrame.Trim();
        }

        return job.Inspection is { FrameEnd: > 0 } inspection
            ? inspection.FrameEnd.ToString()
            : string.Empty;
    }

    private static string ResolveFrameRangeStart(RenderQueueItemViewModel job)
    {
        if (!string.IsNullOrWhiteSpace(job.StartFrame))
        {
            return job.StartFrame.Trim();
        }

        return job.Inspection is { FrameStart: > 0 } inspection
            ? inspection.FrameStart.ToString()
            : string.Empty;
    }

    private static string ResolveFrameStep(RenderQueueItemViewModel job)
    {
        if (!string.IsNullOrWhiteSpace(job.Step))
        {
            return job.Step.Trim();
        }

        return job.Inspection is { FrameStep: > 0 } inspection
            ? inspection.FrameStep.ToString()
            : "1";
    }

    private static string ResolveSingleFrame(RenderQueueItemViewModel job)
    {
        if (!string.IsNullOrWhiteSpace(job.SingleFrame))
        {
            return job.SingleFrame.Trim();
        }

        return job.Inspection is { FrameStart: > 0 } inspection
            ? inspection.FrameStart.ToString()
            : "1";
    }

    private static bool ShouldCreateOverrideScript(RenderQueueItemViewModel job)
    {
        return !string.IsNullOrWhiteSpace(job.CameraName) ||
               !string.IsNullOrWhiteSpace(job.OutputFormat) ||
               !string.IsNullOrWhiteSpace(job.OutputPathTemplate) ||
               !string.IsNullOrWhiteSpace(job.OutputFileNameTemplate) ||
               !string.IsNullOrWhiteSpace(job.ViewLayerName) ||
               (job.UsesOutputFallback && !string.IsNullOrWhiteSpace(job.ResolvedOutputPattern));
    }

    // Helpers

    private void PostToUi(Action action)
    {
        _uiContext.Post(_ => action(), null);
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
        var startFrame = int.TryParse(job.StartFrame.Trim(), out var parsedStartFrame) ? parsedStartFrame : frameNumber;
        var step = int.TryParse(job.Step.Trim(), out var parsedStep) && parsedStep > 0 ? parsedStep : 1;
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

    private sealed record ProcessCommandPlan(
        string ExePath,
        string Arguments,
        string OverrideScriptPath,
        string OutputDirectory,
        bool UsesBlendOutputFallback);

    private bool CanMoveSelectedJobDown()
    {
        return SelectedJob is not null && Jobs.IndexOf(SelectedJob) < Jobs.Count - 1;
    }

    private bool CanMoveSelectedJobUp()
    {
        return SelectedJob is not null && Jobs.IndexOf(SelectedJob) > 0;
    }

    private bool CanOperateOnSelectedJob()
    {
        return SelectedJob is not null;
    }

    private bool CanResetSelectedJob()
    {
        return SelectedJob is not null && !IsQueueRunning && SelectedJob.Status != RenderJobStatus.Rendering;
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
        return !IsQueueRunning && Jobs.Any(static job => job.IsEnabled && job.Status is RenderJobStatus.Pending or RenderJobStatus.Ready or RenderJobStatus.Inspecting);
    }

    private bool CanStopQueue()
    {
        return IsQueueRunning;
    }

    private static bool IsPausedJob(RenderQueueItemViewModel job)
    {
        return job.IsEnabled && job.Status is RenderJobStatus.Canceled;
    }

    private void LoadState()
    {
        var settings = _settingsStore.Load();
        DefaultBlenderPath = settings.DefaultBlenderPath ?? string.Empty;
        LastBlendDirectory = settings.LastBlendDirectory ?? string.Empty;
        LastBlenderDirectory = settings.LastBlenderDirectory ?? string.Empty;

        var queueState = _queueStore.Load();
        Jobs.Clear();
        foreach (var item in queueState.Items)
        {
            Jobs.Add(RenderQueueItemViewModel.FromModel(item));
        }

        SelectedJob = Jobs.FirstOrDefault(job => job.Id == queueState.SelectedJobId) ?? Jobs.FirstOrDefault();
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

        RefreshComputedState();
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshComputedState();
    }

    private void OnSelectedJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedJobLogOutput));
        OnPropertyChanged(nameof(SelectedJobTitle));
        RefreshComputedState();
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(CurrentRunStateText));
        OnPropertyChanged(nameof(GlobalProgressSummary));
        OnPropertyChanged(nameof(GlobalProgressValue));
        OnPropertyChanged(nameof(HasPausedJobs));
        OnPropertyChanged(nameof(QueueSummary));

        InspectSelectedJobCommand.NotifyCanExecuteChanged();
        MoveSelectedJobUpCommand.NotifyCanExecuteChanged();
        MoveSelectedJobDownCommand.NotifyCanExecuteChanged();
        ResetSelectedJobCommand.NotifyCanExecuteChanged();
        ResumeQueueCommand.NotifyCanExecuteChanged();
        StartQueueCommand.NotifyCanExecuteChanged();
        StopQueueCommand.NotifyCanExecuteChanged();
    }

    private string ResolveInitialBlendDirectory()
    {
        if (Directory.Exists(LastBlendDirectory))
        {
            return LastBlendDirectory;
        }

        return string.Empty;
    }

    private string ResolveInitialBlenderDirectory()
    {
        if (Directory.Exists(LastBlenderDirectory))
        {
            return LastBlenderDirectory;
        }

        var currentDefaultDirectory = Path.GetDirectoryName(DefaultBlenderPath);
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

    private RenderQueueItemViewModel? ResolveQueueJobToStart(bool resumePausedJob)
    {
        return resumePausedJob
            ? Jobs.FirstOrDefault(IsPausedJob)
            : Jobs.FirstOrDefault(job => job.IsEnabled
                && job.Status is RenderJobStatus.Pending or RenderJobStatus.Ready or RenderJobStatus.Inspecting);
    }

    private RenderQueueItemViewModel? ResolveRunningJob()
    {
        return Jobs.FirstOrDefault(job => job.Status == RenderJobStatus.Rendering);
    }

    private static string DisplayOrPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusMessage = message;
        StatusTone = tone;
    }
}
