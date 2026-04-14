using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderToolbox.Tools.RenderManager.ViewModels;

public partial class RenderManagerViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly RenderQueueStore _queueStore;
    private readonly RenderManagerSettingsStore _settingsStore;

    public RenderManagerViewModel(
        RenderManagerSettingsStore settingsStore,
        RenderQueueStore queueStore,
        IFilePickerService filePickerService)
    {
        _settingsStore = settingsStore;
        _queueStore = queueStore;
        _filePickerService = filePickerService;

        Jobs.CollectionChanged += OnJobsCollectionChanged;

        LoadState();
        RefreshComputedState();
        SetStatus("Render Manager shell is ready. The layout now tracks per-job and queue-wide progress.", StatusTone.Neutral);
    }

    public ObservableCollection<RenderQueueItemViewModel> Jobs { get; } = [];

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

            return enabledJobs.Average(static job => job.ProgressValue);
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

            var completedJobCount = Jobs.Count(job => job.IsEnabled && job.ProgressValue >= 100);
            return $"{completedJobCount}/{enabledJobCount} completed | {GlobalProgressValue:0}% queue progress";
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

    public string QueueRunButtonLabel => IsQueueRunning
        ? "Stop"
        : HasPausedJobs ? "Resume" : "Start";

    public string QueueSummary => Jobs.Count == 0
        ? "Queue is empty."
        : $"{Jobs.Count(static job => job.IsEnabled)} enabled | {Jobs.Count(static job => !job.IsEnabled)} disabled";

    public string SelectedJobCommandPreview => BuildCommandPreview(SelectedJob);

    public string SelectedJobLogOutput => string.IsNullOrWhiteSpace(SelectedJob?.LogOutput)
        ? "Render logs will appear here once process execution is wired in a later milestone."
        : SelectedJob.LogOutput;

    public string SelectedJobTitle => SelectedJob?.EffectiveName ?? "Select a queue item";

    [ObservableProperty]
    private bool autoInspectOnAdd = true;

    [ObservableProperty]
    private string defaultBlenderPath = string.Empty;

    [ObservableProperty]
    private string defaultOutputFileNameTemplate = "[BLEND_NAME]_[FRAME]";

    [ObservableProperty]
    private string defaultOutputPathTemplate = "[BLEND_PATH]\\renders";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentRunStateText))]
    [NotifyPropertyChangedFor(nameof(QueueRunButtonLabel))]
    [NotifyCanExecuteChangedFor(nameof(ToggleQueueRunCommand))]
    private bool isQueueRunning;

    [ObservableProperty]
    private string lastBlendDirectory = string.Empty;

    [ObservableProperty]
    private string lastBlenderDirectory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedJob))]
    [NotifyPropertyChangedFor(nameof(SelectedJobCommandPreview))]
    [NotifyPropertyChangedFor(nameof(SelectedJobLogOutput))]
    [NotifyPropertyChangedFor(nameof(SelectedJobTitle))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSelectedBlendCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateSelectedJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedJobDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedJobUpCommand))]
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
            AutoInspectOnAdd = AutoInspectOnAdd,
            DefaultBlenderPath = DefaultBlenderPath.Trim(),
            DefaultOutputPathTemplate = DefaultOutputPathTemplate.Trim(),
            DefaultOutputFileNameTemplate = DefaultOutputFileNameTemplate.Trim(),
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
        OnPropertyChanged(nameof(SelectedJobCommandPreview));
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
        }

        RefreshComputedState();
    }

    [RelayCommand]
    private void AddBlend()
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
        var job = RenderQueueItemViewModel.CreateNew(
            selectedPath,
            DefaultBlenderPath,
            DefaultOutputPathTemplate,
            DefaultOutputFileNameTemplate);

        if (AutoInspectOnAdd)
        {
            job.Status = RenderJobStatus.Inspecting;
            job.ProgressText = "Queued for auto-inspection";
            AppendLog(job, "Auto-inspection is enabled and will be wired to a real Blender probe in Milestone 2.");
        }

        Jobs.Add(job);
        SelectedJob = job;
        SetStatus($"Added {job.EffectiveName} to the queue.", StatusTone.Success);
    }

    [RelayCommand]
    private void AddEmptyJob()
    {
        var job = RenderQueueItemViewModel.CreateNew(
            string.Empty,
            DefaultBlenderPath,
            DefaultOutputPathTemplate,
            DefaultOutputFileNameTemplate);

        Jobs.Add(job);
        SelectedJob = job;
        SetStatus("Added an empty queue item. Fill in the blend and targeting fields in the details panel.", StatusTone.Success);
    }

    [RelayCommand]
    private void BrowseDefaultBlender()
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
        SetStatus("Updated the default Blender executable for new jobs.", StatusTone.Success);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnSelectedJob))]
    private void BrowseSelectedBlend()
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

    [RelayCommand(CanExecute = nameof(CanToggleQueueRun))]
    private void ToggleQueueRun()
    {
        if (IsQueueRunning)
        {
            StopQueueShell();
            return;
        }

        StartOrResumeQueueShell();
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

    private string BuildCommandPreview(RenderQueueItemViewModel? job)
    {
        if (job is null)
        {
            return "Select a queue item to preview the future Blender command.";
        }

        var blenderPath = string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? DefaultBlenderPath
            : job.BlenderExecutablePath;

        var arguments = new List<string>
        {
            QuoteOrPlaceholder(blenderPath, "<blender.exe>"),
            "--background",
            QuoteOrPlaceholder(job.BlendFilePath, "<scene.blend>"),
        };

        if (!string.IsNullOrWhiteSpace(job.SceneName))
        {
            arguments.Add("--scene");
            arguments.Add(Quote(job.SceneName.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(job.OutputPathTemplate))
        {
            var outputPattern = $"{job.OutputPathTemplate.Trim()}\\{job.OutputFileNameTemplate.Trim()}";
            arguments.Add("--render-output");
            arguments.Add(Quote(outputPattern));
        }

        switch (job.Mode)
        {
            case RenderMode.Animation:
                arguments.Add("--render-anim");
                break;

            case RenderMode.FrameRange:
                arguments.Add("-s");
                arguments.Add(DisplayOrPlaceholder(job.StartFrame, "<start>"));
                arguments.Add("-e");
                arguments.Add(DisplayOrPlaceholder(job.EndFrame, "<end>"));
                arguments.Add("-j");
                arguments.Add(DisplayOrPlaceholder(job.Step, "1"));
                arguments.Add("-a");
                break;

            case RenderMode.SingleFrame:
                arguments.Add("--render-frame");
                arguments.Add(DisplayOrPlaceholder(job.SingleFrame, "<frame>"));
                break;
        }

        if (!string.IsNullOrWhiteSpace(job.ExtraArgs))
        {
            arguments.Add(job.ExtraArgs.Trim());
        }

        return string.Join(" ", arguments);
    }

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

    private bool CanToggleQueueRun()
    {
        return Jobs.Any(static job => job.IsEnabled);
    }

    private bool IsPausedJob(RenderQueueItemViewModel job)
    {
        return job.ProgressValue > 0
            && job.ProgressValue < 100
            && job.Status is RenderJobStatus.Canceled or RenderJobStatus.Stopping;
    }

    private void LoadState()
    {
        var settings = _settingsStore.Load();
        AutoInspectOnAdd = settings.AutoInspectOnAdd;
        DefaultBlenderPath = settings.DefaultBlenderPath ?? string.Empty;
        DefaultOutputPathTemplate = string.IsNullOrWhiteSpace(settings.DefaultOutputPathTemplate)
            ? "[BLEND_PATH]\\renders"
            : settings.DefaultOutputPathTemplate;
        DefaultOutputFileNameTemplate = string.IsNullOrWhiteSpace(settings.DefaultOutputFileNameTemplate)
            ? "[BLEND_NAME]_[FRAME]"
            : settings.DefaultOutputFileNameTemplate;
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
        OnPropertyChanged(nameof(SelectedJobCommandPreview));
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
        OnPropertyChanged(nameof(QueueRunButtonLabel));
        OnPropertyChanged(nameof(QueueSummary));

        MoveSelectedJobUpCommand.NotifyCanExecuteChanged();
        MoveSelectedJobDownCommand.NotifyCanExecuteChanged();
        ToggleQueueRunCommand.NotifyCanExecuteChanged();
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

    private RenderQueueItemViewModel? ResolveQueueJobToStart()
    {
        if (SelectedJob is { IsEnabled: true } selectedJob && selectedJob.Status != RenderJobStatus.Completed)
        {
            return selectedJob;
        }

        return Jobs.FirstOrDefault(IsPausedJob)
            ?? Jobs.FirstOrDefault(job => job.IsEnabled && job.Status != RenderJobStatus.Completed);
    }

    private RenderQueueItemViewModel? ResolveRunningJob()
    {
        return Jobs.FirstOrDefault(job => job.Status == RenderJobStatus.Rendering);
    }

    private void StartOrResumeQueueShell()
    {
        var job = ResolveQueueJobToStart();
        if (job is null)
        {
            SetStatus("Add at least one enabled job before starting the queue.", StatusTone.Error);
            return;
        }

        var isResume = HasPausedJobs || IsPausedJob(job);
        SelectedJob = job;
        job.Status = RenderJobStatus.Rendering;
        if (job.ProgressValue <= 0)
        {
            job.ProgressValue = 12;
        }

        job.ProgressText = $"Running {job.ProgressPercentLabel}";
        job.ElapsedText = job.ElapsedText.Length == 0 ? "00:00:12" : job.ElapsedText;
        job.EtaText = job.EtaText.Length == 0 ? "ETA pending" : job.EtaText;
        job.LastStartedUtc = DateTimeOffset.Now;
        AppendLog(job, "Queue shell started this job. The real render pipeline will reuse the same start/stop/resume position.");

        IsQueueRunning = true;
        RefreshComputedState();
        SetStatus(
            isResume
                ? $"Resumed {job.EffectiveName} from the same queue slot."
                : $"Started the queue with {job.EffectiveName}.",
            StatusTone.Success);
    }

    private void StopQueueShell()
    {
        var runningJob = ResolveRunningJob();
        if (runningJob is null)
        {
            IsQueueRunning = false;
            RefreshComputedState();
            SetStatus("Queue was already idle.", StatusTone.Neutral);
            return;
        }

        runningJob.Status = RenderJobStatus.Canceled;
        runningJob.ProgressText = $"Paused at {runningJob.ProgressPercentLabel}";
        AppendLog(runningJob, "Queue shell paused this job. Resume will continue from the same slot.");

        IsQueueRunning = false;
        RefreshComputedState();
        SetStatus($"Paused {runningJob.EffectiveName}. Use the same button to resume from here.", StatusTone.Neutral);
    }

    private static string DisplayOrPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string QuoteOrPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : Quote(value.Trim());
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusMessage = message;
        StatusTone = tone;
    }
}
