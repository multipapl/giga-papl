using System.Text;
using System.Windows.Media.Imaging;
using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobRuntimeViewModel : ObservableObject
{
    public const string DefaultPreviewStatusText = "Preview will appear after the first saved frame.";
    public const string StoredPreviewStatusText = "Preview can be reloaded from the last saved frame.";

    private readonly StringBuilder _pendingLogOutput = new();
    private DateTimeOffset _lastPreviewDecodeUtc = DateTimeOffset.MinValue;

    public string ProgressPercentLabel => $"{ProgressValue:0}%";

    public bool HasPreviewImage => PreviewImageSource is not null;

    public string PreviewPathText => string.IsNullOrWhiteSpace(LastKnownOutputPath)
        ? string.IsNullOrWhiteSpace(LastKnownOutputFolderPath)
            ? "Waiting for the first saved frame."
            : LastKnownOutputFolderPath.Trim()
        : LastKnownOutputPath.Trim();

    public string LastErrorSummaryText => string.IsNullOrWhiteSpace(LastErrorSummary)
        ? "No validation or runtime errors recorded."
        : LastErrorSummary.Trim();

    public void AppendBufferedLogLine(string line)
    {
        if (_pendingLogOutput.Length > 0)
        {
            _pendingLogOutput.AppendLine();
        }

        _pendingLogOutput.Append(line);
    }

    public bool FlushLogBuffer()
    {
        if (_pendingLogOutput.Length == 0)
        {
            return false;
        }

        LogOutput = string.IsNullOrWhiteSpace(LogOutput)
            ? _pendingLogOutput.ToString()
            : $"{LogOutput}{Environment.NewLine}{_pendingLogOutput}";
        _pendingLogOutput.Clear();
        return true;
    }

    public bool CanDecodePreviewNow(DateTimeOffset now, TimeSpan throttle)
    {
        if (!IsSelected)
        {
            return false;
        }

        if (now - _lastPreviewDecodeUtc < throttle)
        {
            return false;
        }

        _lastPreviewDecodeUtc = now;
        return true;
    }

    public void ResetRuntimeState(string? blendFilePath, string? lifecycleMessage = null)
    {
        ProgressValue = 0;
        ProgressText = "Waiting";
        ElapsedText = string.Empty;
        EtaText = string.Empty;
        PreviewImageSource = null;
        PreviewStatusText = DefaultPreviewStatusText;
        LastKnownOutputPath = string.Empty;
        LastKnownOutputFolderPath = string.Empty;
        LastErrorSummary = string.Empty;
        CompletedFrameRenderSeconds = 0;
        ResumeCompletedFrameCount = 0;
        LastReportedFrameNumber = 0;
        LastCompletedFrameNumber = 0;
        LastStartedUtc = null;
        LastCompletedUtc = null;
        LogOutput = BuildLifecycleLog(lifecycleMessage ?? "Queue item reset.");
        Status = GetInitialStatus(blendFilePath);
    }

    [ObservableProperty]
    private double completedFrameRenderSeconds;

    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string etaText = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private DateTimeOffset? lastCompletedUtc;

    [ObservableProperty]
    private int lastCompletedFrameNumber;

    [ObservableProperty]
    private string lastErrorSummary = string.Empty;

    [ObservableProperty]
    private string lastKnownOutputPath = string.Empty;

    [ObservableProperty]
    private string lastKnownOutputFolderPath = string.Empty;

    [ObservableProperty]
    private string lastLogFilePath = string.Empty;

    [ObservableProperty]
    private int lastReportedFrameNumber;

    [ObservableProperty]
    private DateTimeOffset? lastStartedUtc;

    [ObservableProperty]
    private string logOutput = string.Empty;

    [ObservableProperty]
    private BitmapSource? previewImageSource;

    [ObservableProperty]
    private string previewStatusText = DefaultPreviewStatusText;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string progressText = "Waiting";

    [ObservableProperty]
    private int resumeCompletedFrameCount;

    [ObservableProperty]
    private RenderJobStatus status = RenderJobStatus.Pending;

    partial void OnLastErrorSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(LastErrorSummaryText));
    }

    partial void OnLastKnownOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewPathText));
    }

    partial void OnLastKnownOutputFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewPathText));
    }

    partial void OnPreviewImageSourceChanged(BitmapSource? value)
    {
        OnPropertyChanged(nameof(HasPreviewImage));
    }

    partial void OnProgressValueChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressPercentLabel));
    }

    private static RenderJobStatus GetInitialStatus(string? blendFilePath)
    {
        return string.IsNullOrWhiteSpace(blendFilePath)
            ? RenderJobStatus.Pending
            : RenderJobStatus.Ready;
    }

    private static string BuildLifecycleLog(string message)
    {
        return $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}";
    }
}
