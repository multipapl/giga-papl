using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobOutputViewModel : ObservableObject
{
    public bool HasOutputNameOverride
    {
        get => OutputFileNameOverrideEnabled;
        set => SetOutputNameOverride(value, OutputFileNameTemplate);
    }

    public bool HasOutputPathOverride
    {
        get => OutputPathOverrideEnabled;
        set => SetOutputPathOverride(value, OutputPathTemplate);
    }

    public void SetOutputNameOverride(bool enabled, string resolvedOutputName)
    {
        if (enabled == HasOutputNameOverride)
        {
            return;
        }

        OutputFileNameOverrideEnabled = enabled;
        OutputFileNameTemplate = enabled ? resolvedOutputName : string.Empty;
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    public void SetOutputPathOverride(bool enabled, string resolvedOutputDirectory)
    {
        if (enabled == HasOutputPathOverride)
        {
            return;
        }

        OutputPathOverrideEnabled = enabled;
        OutputPathTemplate = enabled ? resolvedOutputDirectory : string.Empty;
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }

    [ObservableProperty]
    private bool outputFileNameOverrideEnabled;

    [ObservableProperty]
    private string outputFileNameTemplate = string.Empty;

    [ObservableProperty]
    private bool outputPathOverrideEnabled;

    [ObservableProperty]
    private string outputPathTemplate = string.Empty;

    partial void OnOutputFileNameOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    partial void OnOutputFileNameTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    partial void OnOutputPathOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }

    partial void OnOutputPathTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }
}
