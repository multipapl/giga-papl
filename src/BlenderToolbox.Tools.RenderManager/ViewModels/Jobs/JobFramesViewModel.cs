using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobFramesViewModel : ObservableObject
{
    public bool HasStoredFrameOverride => Mode != RenderMode.FrameRange
        || !string.IsNullOrWhiteSpace(StartFrame)
        || !string.IsNullOrWhiteSpace(EndFrame)
        || !string.IsNullOrWhiteSpace(SingleFrame)
        || !string.IsNullOrWhiteSpace(Step);

    public bool HasFrameOverride
    {
        get => FrameOverrideEnabled || HasStoredFrameOverride;
        set
        {
            if (value == HasFrameOverride)
            {
                return;
            }

            FrameOverrideEnabled = value;
            if (!value)
            {
                Mode = RenderMode.FrameRange;
                StartFrame = string.Empty;
                EndFrame = string.Empty;
                SingleFrame = string.Empty;
                Step = string.Empty;
            }
            else
            {
                Mode = RenderMode.FrameRange;
            }

            OnPropertyChanged(nameof(HasFrameOverride));
        }
    }

    public bool IsAnimationMode => Mode == RenderMode.Animation;

    public bool IsFrameRangeMode => Mode == RenderMode.FrameRange;

    public bool IsSingleFrameMode => Mode == RenderMode.SingleFrame;

    public void ApplyBlendFrameDefaults(BlendInspectionSnapshot? inspection)
    {
        if (inspection is null)
        {
            return;
        }

        StartFrame = inspection.FrameStart > 0 ? inspection.FrameStart.ToString() : StartFrame;
        EndFrame = inspection.FrameEnd > 0 ? inspection.FrameEnd.ToString() : EndFrame;
        Step = inspection.FrameStep > 0 ? inspection.FrameStep.ToString() : "1";
    }

    [ObservableProperty]
    private string endFrame = string.Empty;

    [ObservableProperty]
    private bool frameOverrideEnabled;

    [ObservableProperty]
    private RenderMode mode = RenderMode.FrameRange;

    [ObservableProperty]
    private string singleFrame = string.Empty;

    [ObservableProperty]
    private string startFrame = string.Empty;

    [ObservableProperty]
    private string step = string.Empty;

    partial void OnFrameOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasFrameOverride));
    }

    partial void OnEndFrameChanged(string value)
    {
        SyncFrameOverrideFlag();
    }

    partial void OnModeChanged(RenderMode value)
    {
        SyncFrameOverrideFlag();
        OnPropertyChanged(nameof(HasFrameOverride));
        OnPropertyChanged(nameof(IsAnimationMode));
        OnPropertyChanged(nameof(IsFrameRangeMode));
        OnPropertyChanged(nameof(IsSingleFrameMode));
    }

    partial void OnSingleFrameChanged(string value)
    {
        SyncFrameOverrideFlag();
    }

    partial void OnStartFrameChanged(string value)
    {
        SyncFrameOverrideFlag();
    }

    partial void OnStepChanged(string value)
    {
        SyncFrameOverrideFlag();
    }

    private void SyncFrameOverrideFlag()
    {
        var hasOverride = HasStoredFrameOverride;
        if (FrameOverrideEnabled != hasOverride)
        {
            FrameOverrideEnabled = hasOverride;
        }
        else
        {
            OnPropertyChanged(nameof(HasFrameOverride));
        }
    }
}
