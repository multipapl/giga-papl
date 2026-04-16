using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobFramesViewModel : ObservableObject
{
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
                Step = string.IsNullOrWhiteSpace(Step) ? "1" : Step;
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
    private RenderMode mode = RenderMode.Animation;

    [ObservableProperty]
    private string singleFrame = string.Empty;

    [ObservableProperty]
    private string startFrame = string.Empty;

    [ObservableProperty]
    private string step = "1";

    partial void OnFrameOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasFrameOverride));
    }

    partial void OnModeChanged(RenderMode value)
    {
        OnPropertyChanged(nameof(HasFrameOverride));
        OnPropertyChanged(nameof(IsAnimationMode));
        OnPropertyChanged(nameof(IsFrameRangeMode));
        OnPropertyChanged(nameof(IsSingleFrameMode));
    }
}
